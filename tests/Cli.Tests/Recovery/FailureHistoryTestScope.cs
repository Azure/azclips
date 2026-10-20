// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Configuration.Abstractions;
using Core.Configuration.Implementation;
using Core.Configuration.Schema;
using Core.Runtime.Recovery;

namespace Cli.Tests.Recovery;

internal sealed class FailureHistoryTestScope : IDisposable
{
    /// <param name="trigger">Stored agent.recovery value. History tests run an enabled store by default.</param>
    internal FailureHistoryTestScope(
        TimeSpan? lockTimeout = null, Action<string, string>? replace = null, Action<string>? delete = null,
        string? trigger = "manual")
    {
        Root = Path.GetFullPath(Path.Combine(".test-artifacts", "failure-history-v2", Guid.NewGuid().ToString("N")));
        Files = new FailureHistoryFiles(Path.Combine(Root, "failure-history"), lockTimeout, replace, delete);
        State = new FailureHistoryStateStore(Files);
        Provider = new PathConfigurationProvider(Path.Combine(Root, "config.json"));
        if (trigger is not null)
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Provider.GetConfigFilePath(), "{\"agent\":{\"recovery\":{\"trigger\":\"" + trigger + "\"}}}");
        }
        Configuration = CreateConfiguration();
    }

    internal string Root { get; }
    internal string DirectoryPath => Files.DirectoryPath;
    internal FailureHistoryFiles Files { get; }
    internal FailureHistoryStateStore State { get; }
    internal IConfigurationProvider Provider { get; }
    internal ConfigurationService Configuration { get; }

    internal ConfigurationService CreateConfiguration(IConfigurationProvider? provider = null)
    {
        var schema = new ConfigurationSchema();
        return new ConfigurationService(provider ?? Provider, schema, new DefaultConfigurationValidator(schema),
            new RecoveryConfigurationCoordinator(State));
    }

    internal EncryptedFailureHistoryStore CreateStore(IFailureHistoryKeyStore? keys = null, IConfigurationService? configuration = null) =>
        new(keys ?? new TestFailureHistoryKeys(), configuration ?? Configuration, State);

    internal static FailureRecord CreateRecord(ulong generation = 0, uint pid = 48213) => new()
    {
        TerminalSessionId = pid,
        HistoryGeneration = generation,
        OccurredAtUtc = new DateTimeOffset(2026, 9, 23, 10, 4, 0, TimeSpan.Zero),
        Arguments = ["group", "create", "--name", "private-resource-name"],
        FailedCommand = "az group create --name private-resource-name",
        ErrorText = "The requested operation failed.",
        ExitCode = 1,
    };

    internal static FailureRecord Committed(FailureRecord record, FailureHistoryEntry entry)
    {
        Assert.True(FailureId.TryParse(entry.Id, out var id));
        return record with { Id = entry.Id, Sequence = id.Sequence };
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }
}

internal sealed class TestFailureHistoryKeys(byte[]? key = null) : IFailureHistoryKeyStore
{
    internal byte[] Key { get; } = key ?? [.. Enumerable.Repeat((byte)0x31, 32)];
    internal ConcurrentQueue<bool> CreateRequests { get; } = new();
    internal ConcurrentQueue<ulong> Generations { get; } = new();
    internal ConcurrentBag<byte[]> ReturnedKeys { get; } = [];
    internal Func<ulong, bool, CancellationToken, Task<byte[]>>? Read { get; init; }

    public async Task<byte[]> GetKeyAsync(ulong generation, bool createIfMissing, CancellationToken cancellationToken = default)
    {
        CreateRequests.Enqueue(createIfMissing);
        Generations.Enqueue(generation);
        var result = Read is not null ? await Read(generation, createIfMissing, cancellationToken)
            : generation == 0 ? Key.ToArray() : SHA256.HashData([.. Key, .. BitConverter.GetBytes(generation)]);
        ReturnedKeys.Add(result);
        return result;
    }
}

internal sealed class PathConfigurationProvider(string configFilePath) : IConfigurationProvider
{
    public string GetConfigFilePath() => configFilePath;

    public async Task<System.Text.Json.Nodes.JsonObject> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(configFilePath))
            return [];
        var json = await File.ReadAllTextAsync(configFilePath, cancellationToken);
        return string.IsNullOrWhiteSpace(json) ? [] : System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject ?? [];
    }

    public async Task WriteAsync(System.Text.Json.Nodes.JsonObject configuration, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configFilePath)!);
        var temp = configFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temp, configuration.ToJsonString(), cancellationToken);
        File.Move(temp, configFilePath, overwrite: true);
    }
}
