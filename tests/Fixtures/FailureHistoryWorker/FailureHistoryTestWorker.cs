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
        // Stands in for the CLI history worker: <mode> <input pipe handle>.
        if (args.Length == 2)
            return await RunStderrModeAsync(args[0]);
        if (args.Length != 4)
            return 64;
        var (directory, mode, ready, release) = (args[0], args[1], args[2], args[3]);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = deadline.Token;
        // Cold processes queue on the lock longer than the production two-second budget.
        var files = new FailureHistoryFiles(directory, lockTimeout: TimeSpan.FromSeconds(10), replace: (source, target) =>
        {
            if (mode == "crash-before-replace")
            {
                File.WriteAllText(ready, "reserved");
                while (!File.Exists(release))
                {
                    token.ThrowIfCancellationRequested();
                    Thread.Sleep(10);
                }
            }
            if (File.Exists(target))
                File.Replace(source, target, destinationBackupFileName: null);
            else
                File.Move(source, target);
        });
        var store = new EncryptedFailureHistoryStore(new OfflineKeyStore(), files);
        try
        {
            if (mode != "crash-before-replace")
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

    private static async Task<int> RunStderrModeAsync(string mode)
    {
        var stderr = Console.Error;
        switch (mode)
        {
            case "stderr-flood":
                // 8 MB without a newline, then a structured result on stdout.
                var chunk = new string('x', 64 * 1024);
                for (var i = 0; i < 128; i++)
                    stderr.Write(chunk);
                stderr.Flush();
                Console.Out.Write("""{"Entry":null,"Code":"flood_done","Message":"flood done"}""");
                return 1;
            case "stderr-hang":
                stderr.Write("partial-diagnostic-before-hang");
                stderr.Flush();
                await Task.Delay(Timeout.Infinite);
                return 0;
            case "stderr-utf8-hang":
                // 4096 bytes: fewer chars than one 4096-char read request.
                var raw = Console.OpenStandardError();
                raw.Write(System.Text.Encoding.UTF8.GetBytes(new string('é', 2048)));
                raw.Flush();
                await Task.Delay(Timeout.Infinite);
                return 0;
            case "stderr-early-exit":
                stderr.Write("diagnostic-before-exit");
                stderr.Flush();
                return 3;
            default:
                return 64;
        }
    }

    private sealed class OfflineKeyStore : IFailureHistoryKeyStore
    {
        public Task<byte[]> GetKeyAsync(bool createIfMissing, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]>([.. Enumerable.Repeat((byte)0x31, 32)]);
    }
}
