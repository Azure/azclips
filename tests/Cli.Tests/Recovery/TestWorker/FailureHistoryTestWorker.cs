// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Configuration.Implementation;
using Core.Configuration.Schema;
using Core.Runtime.Recovery;

namespace Cli.Tests.Recovery.Worker;

internal static class FailureHistoryTestWorker
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 4)
            return 64;
        var (home, mode, ready, release) = (args[0], args[1], args[2], args[3]);
        Environment.SetEnvironmentVariable(HomePaths.HomeOverrideVariable, home);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = deadline.Token;
        // This fixture tests allocator linearizability under 20 cold processes, separately
        // from the production two-second capture budget and the bounded-lock unit tests.
        var files = new FailureHistoryFiles(lockTimeout: TimeSpan.FromSeconds(10), replace: (source, target) =>
        {
            if (mode == "crash-after-reservation" && Path.GetFileName(target) == EncryptedFailureHistoryStore.HistoryFileName)
            {
                File.WriteAllText(ready, "reserved");
                while (!File.Exists(release))
                {
                    token.ThrowIfCancellationRequested();
                    Thread.Sleep(10);
                }
            }
            File.Move(source, target, overwrite: true);
        });
        var state = new FailureHistoryStateStore(files);
        var schema = new ConfigurationSchema();
        var config = new ConfigurationService(new FileConfigurationProvider(), schema, new DefaultConfigurationValidator(schema),
            new RecoveryConfigurationCoordinator(state));
        var store = new EncryptedFailureHistoryStore(new OfflineKeyStore(), config, state);
        try
        {
            if (mode is "off" or "manual")
            {
                await config.SetConfigurationAsync("agent.recovery", mode, cancellationToken: token);
                return 0;
            }

            var generation = await store.ReadGenerationAsync(token);
            if (mode != "crash-after-reservation")
            {
                _ = config.GetConfiguration<AgentOptions>().Recovery.IsEnabled;
                await File.WriteAllTextAsync(ready, generation.ToString(), token);
                while (!File.Exists(release))
                    await Task.Delay(10, token);
            }
            var entry = await store.SaveAsync(new FailureRecord
            {
                TerminalSessionId = 48213,
                HistoryGeneration = generation,
                OccurredAtUtc = new DateTimeOffset(2026, 9, 23, 10, 4, 0, TimeSpan.Zero),
                Arguments = ["test", "offline-worker"],
                FailedCommand = "az test offline-worker",
                ErrorText = "synthetic-offline-worker-failure",
                ExitCode = 1,
            }, 20, token);
            Console.WriteLine(JsonSerializer.Serialize(entry));
            return 0;
        }
        catch (FailureHistoryException exception)
        {
            Console.Error.WriteLine(exception.Code);
            return 23;
        }
    }

    private sealed class OfflineKeyStore : IFailureHistoryKeyStore
    {
        public Task<byte[]> GetKeyAsync(ulong generation, bool createIfMissing, CancellationToken cancellationToken = default)
        {
            byte[] key = [.. Enumerable.Repeat((byte)0x31, 32)];
            if (generation == 0)
                return Task.FromResult(key);
            var derived = SHA256.HashData([.. key, .. BitConverter.GetBytes(generation)]);
            CryptographicOperations.ZeroMemory(key);
            return Task.FromResult(derived);
        }
    }
}
