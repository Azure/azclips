// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Core.Abstractions.Recovery;
using static Cli.Tests.Recovery.FailureHistoryTestScope;

namespace Cli.Tests.Recovery;

[Collection("Azclips Service Provider Tests")]
public sealed class FailureHistoryConcurrencyTests
{
    [Fact]
    public async Task TwentyRealProcesses_FrozenPidAndMinuteAllocateUniqueIds()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.Configuration.SetConfigurationAsync("agent.recovery.historySize", 20);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var release = Path.Combine(scope.Root, "release");
        var workers = Enumerable.Range(0, 20).Select(index => StartWorker(scope, "save", $"ready-{index}", release)).ToArray();
        try
        {
            await Task.WhenAll(workers.Select(worker => WaitForReadyAsync(worker, deadline.Token)));
            await File.WriteAllTextAsync(release, "go", deadline.Token);
            var results = await Task.WhenAll(workers.Select(worker => worker.CompleteAsync(deadline.Token)));
            Assert.All(results, result => Assert.True(result.ExitCode == 0, result.Error));
            var entries = results.Select(result => JsonSerializer.Deserialize<FailureHistoryEntry>(result.Output)!).ToArray();
            Assert.Equal(20, entries.Select(entry => entry.Id).Distinct().Count());
            Assert.All(entries, entry => Assert.StartsWith("48213-2026-0923-1004-", entry.Id));
            var records = await scope.CreateStore().ReadRecordsAsync();
            Assert.Equal(20, records.Count);
            Assert.Equal(Enumerable.Range(0, 20).Reverse().Select(index => (ulong)index), records.Select(record => record.Sequence));
            Assert.Equal(entries.Select(entry => entry.Id).Order(), records.Select(record => record.Id).Order());
        }
        finally { foreach (var worker in workers) await worker.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessBarrier_PreOffCaptureCannotRecreateHistory(bool reenable)
    {
        using var scope = new FailureHistoryTestScope();
        Directory.CreateDirectory(scope.Root);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var release = Path.Combine(scope.Root, "release");
        await using var stale = StartWorker(scope, "save", "stale-ready", release);
        await WaitForReadyAsync(stale, deadline.Token);
        await RunConfigWorkerAsync(scope, "off", deadline.Token);
        if (reenable)
            await RunConfigWorkerAsync(scope, "manual", deadline.Token);
        await File.WriteAllTextAsync(release, "go", deadline.Token);
        var result = await stale.CompleteAsync(deadline.Token);
        Assert.Equal(23, result.ExitCode);
        Assert.Contains(reenable ? "failure_history_stale_generation" : "failure_history_disabled", result.Error);
        var store = scope.CreateStore();
        Assert.Empty(await store.ReadIndexAsync());
        if (reenable)
            await store.SaveAsync(CreateRecord(await store.ReadGenerationAsync()), 10);
    }

    [Fact]
    public async Task KilledReservedWriter_LeavesCiphertextOnlyAndTheReservationIsNeverReused()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        var originalEntry = await store.SaveAsync(CreateRecord(), 10);
        var original = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using (var writer = StartWorker(scope, "crash-after-reservation", "reserved", Path.Combine(scope.Root, "never-release")))
        {
            await WaitForReadyAsync(writer, deadline.Token);
            writer.Process.Kill(entireProcessTree: true);
            await writer.Process.WaitForExitAsync(deadline.Token);
        }
        Assert.Equal(original, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
        Assert.Equal(originalEntry.Id, Assert.Single(await store.ReadRecordsAsync()).Id);
        var staging = Assert.Single(Directory.GetFiles(scope.DirectoryPath, ".history-v2-*.tmp"));
        var bytes = await File.ReadAllBytesAsync(staging);
        Assert.True(bytes.AsSpan().IndexOf("synthetic-offline-worker-failure"u8) < 0);
        var entry = await store.SaveAsync(CreateRecord(), 10);
        Assert.True(FailureId.TryParse(entry.Id, out var id));
        Assert.Equal(2UL, id.Sequence);
        await store.ClearAsync();
        Assert.False(File.Exists(staging));
    }

    private static async Task RunConfigWorkerAsync(FailureHistoryTestScope scope, string trigger, CancellationToken token)
    {
        await using var worker = StartWorker(scope, trigger, "unused", Path.Combine(scope.Root, "unused-release"));
        var result = await worker.CompleteAsync(token);
        Assert.True(result.ExitCode == 0, result.Error);
    }

    private static async Task WaitForReadyAsync(WorkerProcess worker, CancellationToken token)
    {
        while (!File.Exists(worker.ReadyPath))
        {
            if (worker.Process.HasExited)
            {
                var result = await worker.CompleteAsync(token);
                Assert.Fail($"History worker exited before its barrier ({result.ExitCode}): {result.Error}");
            }
            await Task.Delay(10, token);
        }
    }

    private static WorkerProcess StartWorker(FailureHistoryTestScope scope, string mode, string readyName, string release)
    {
        var root = Path.GetDirectoryName(typeof(FailureHistoryConcurrencyTests).Assembly.Location);
        while (root is not null && !File.Exists(Path.Combine(root, "azclips.slnx")))
            root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        var configuration = typeof(FailureHistoryConcurrencyTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var workerPath = Path.Combine(root, "tests", "Cli.Tests", "Recovery", "TestWorker", "bin", configuration, "net10.0", "FailureHistory.TestWorker.dll");
        Assert.True(File.Exists(workerPath), $"Build the offline history worker fixture first: {workerPath}");
        var ready = Path.Combine(scope.Root, readyName);
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = root,
        };
        foreach (var variable in new[] { "AZCLIPS_AGENT_RECOVERY", "AZCLIPS_AGENT_RECOVERY_STYLE", "AZCLIPS_AGENT_RECOVERY_HISTORY_SIZE" })
            start.Environment.Remove(variable);
        foreach (var argument in new[] { workerPath, scope.Root, mode, ready, release })
            start.ArgumentList.Add(argument);
        return new WorkerProcess(Process.Start(start)!, ready);
    }

    private sealed class WorkerProcess(Process process, string readyPath) : IAsyncDisposable
    {
        private readonly Task<string> _output = process.StandardOutput.ReadToEndAsync();
        private readonly Task<string> _error = process.StandardError.ReadToEndAsync();
        internal Process Process { get; } = process;
        internal string ReadyPath { get; } = readyPath;
        internal async Task<(int ExitCode, string Output, string Error)> CompleteAsync(CancellationToken token)
        {
            await Process.WaitForExitAsync(token);
            return (Process.ExitCode, await _output, await _error);
        }

        public async ValueTask DisposeAsync()
        {
            if (!Process.HasExited)
                Process.Kill(entireProcessTree: true);
            await Process.WaitForExitAsync();
            await Task.WhenAll(_output, _error);
            Process.Dispose();
        }
    }
}
