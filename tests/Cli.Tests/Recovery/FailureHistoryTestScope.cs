// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Core.Abstractions.Recovery;
using Core.Runtime.Recovery;

namespace Cli.Tests.Recovery;

internal sealed class FailureHistoryTestScope : IDisposable
{
    internal FailureHistoryTestScope(
        TimeSpan? lockTimeout = null, Action<string, string>? replace = null, Action<string>? delete = null)
    {
        Root = Path.GetFullPath(Path.Combine(".test-artifacts", "failure-history", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Root);
        Files = new FailureHistoryFiles(Path.Combine(Root, "failure-history"), lockTimeout, replace, delete);
    }

    internal string Root { get; }
    internal string DirectoryPath => Files.DirectoryPath;
    internal FailureHistoryFiles Files { get; }

    internal EncryptedFailureHistoryStore CreateStore(IFailureHistoryKeyStore? keys = null) =>
        new(keys ?? new TestFailureHistoryKeys(), Files);

    internal static FailureRecord CreateRecord(uint pid = 48213) => new()
    {
        TerminalSessionId = pid,
        OccurredAtUtc = new DateTimeOffset(2026, 9, 23, 10, 4, 0, TimeSpan.Zero),
        Arguments = ["group", "create", "--name", "private-resource-name"],
        FailedCommand = "az group create --name private-resource-name",
        ErrorText = "The requested operation failed.",
        ExitCode = 1,
    };

    internal static FailureRecord Committed(FailureRecord record, FailureHistoryEntry entry, ulong sequence)
    {
        Assert.True(FailureId.TryParse(entry.Id, out var id));
        Assert.Equal(record.TerminalSessionId, id.TerminalSessionId);
        return record with { Id = entry.Id, Sequence = sequence };
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
    internal ConcurrentBag<byte[]> ReturnedKeys { get; } = [];
    internal Func<bool, CancellationToken, Task<byte[]>>? Read { get; init; }

    internal static TestFailureHistoryKeys Unavailable() =>
        new() { Read = (_, _) => throw new FailureHistoryException("failure_history_key_unavailable", "Secure storage is unavailable.") };

    public async Task<byte[]> GetKeyAsync(bool createIfMissing, CancellationToken cancellationToken = default)
    {
        CreateRequests.Enqueue(createIfMissing);
        var result = Read is not null ? await Read(createIfMissing, cancellationToken) : Key.ToArray();
        ReturnedKeys.Add(result);
        return result;
    }
}
