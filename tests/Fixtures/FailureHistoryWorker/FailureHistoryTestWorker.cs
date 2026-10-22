// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Recovery;
using Core.Runtime.Recovery;

namespace Cli.Tests.Recovery.Worker;

internal static class FailureHistoryTestWorker
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 4)
            return 64;
        var (directory, mode, ready, release) = (args[0], args[1], args[2], args[3]);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = deadline.Token;
        // This fixture tests allocator linearizability under 20 cold processes, separately
        // from the production two-second capture budget and the bounded-lock unit tests.
        var files = new FailureHistoryFiles(directory, lockTimeout: TimeSpan.FromSeconds(10), replace: (source, target) =>
        {
            if ((mode == "crash-after-reservation" && Path.GetFileName(target) == EncryptedFailureHistoryStore.HistoryFileName) ||
                (mode == "crash-plaintext" && Path.GetFileName(target) == EncryptedFailureHistoryStore.PlaintextHistoryFileName))
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
        var store = new EncryptedFailureHistoryStore(
            mode == "crash-plaintext" ? new UnavailableKeyStore() : new OfflineKeyStore(), new FailureHistoryStateStore(files));
        try
        {
            if (mode is not ("crash-after-reservation" or "crash-plaintext"))
            {
                await File.WriteAllTextAsync(ready, "ready", token);
                while (!File.Exists(release))
                    await Task.Delay(10, token);
            }
            var entry = await store.SaveAsync(new FailureRecord
            {
                TerminalSessionId = 48213,
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

    private sealed class UnavailableKeyStore : IFailureHistoryKeyStore
    {
        public Task<byte[]> GetKeyAsync(bool createIfMissing, CancellationToken cancellationToken = default) =>
            throw new FailureHistoryException("failure_history_key_unavailable", "Secure storage is unavailable.");
    }

    private sealed class OfflineKeyStore : IFailureHistoryKeyStore
    {
        public Task<byte[]> GetKeyAsync(bool createIfMissing, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]>([.. Enumerable.Repeat((byte)0x31, 32)]);
    }
}
