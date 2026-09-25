// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Configuration.Abstractions;
using Core.Runtime.Recovery;
using static Cli.Tests.Recovery.FailureHistoryTestScope;

namespace Cli.Tests.Recovery;

[Collection("Azclips Service Provider Tests")]
public sealed class FailureHistoryResetTests
{
    [Fact]
    public async Task RetentionOne_AlternatingShellsAndResetNeverReuseIds()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.Configuration.SetConfigurationAsync("agent.recovery.historySize", 1);
        var store = scope.CreateStore();
        var entries = new List<FailureHistoryEntry>();
        foreach (var pid in new uint[] { 2, 1, 2 })
            entries.Add(await store.SaveAsync(CreateRecord(pid: pid), 1));
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "manual");
        entries.Add(await store.SaveAsync(CreateRecord(await store.ReadGenerationAsync(), pid: 2), 1));
        Assert.Equal(4, entries.Select(entry => entry.Id).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, 4).Select(index => (ulong)index),
            entries.Select(entry => { Assert.True(FailureId.TryParse(entry.Id, out var parsed)); return parsed.Sequence; }));
        Assert.Equal(entries[^1], Assert.Single(await store.ReadIndexAsync()));
    }

    [Fact]
    public async Task CounterWidthGrowthAndExhaustion_DoNotWrapOrReplaceHistory()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        await store.SaveAsync(CreateRecord(), 10);
        var state = (await scope.State.ReadAsync(default))!;
        await scope.State.WriteAsync(state with { NextSequence = 9999 }, default);
        Assert.EndsWith("-9999", (await store.SaveAsync(CreateRecord(), 10)).Id);
        Assert.EndsWith("-10000", (await store.SaveAsync(CreateRecord(), 10)).Id);
        var snapshot = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        await scope.State.WriteAsync(state with { NextSequence = ulong.MaxValue }, default);
        Assert.Equal("failure_history_sequence_exhausted",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10))).Code);
        Assert.Equal(snapshot, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
        await store.ClearAsync();
        Assert.Equal(ulong.MaxValue, (await scope.State.ReadAsync(default))!.NextSequence);
    }

    [Fact]
    public async Task V1Bytes_AreIncompatibleAndPreservedUntilExplicitKeylessReset()
    {
        using var scope = new FailureHistoryTestScope();
        Directory.CreateDirectory(scope.DirectoryPath);
        var path = Path.Combine(scope.DirectoryPath, EncryptedFailureHistoryStore.LegacyHistoryFileName);
        byte[] old = [65, 90, 67, 76, 70, 72, 83, 84, 1, 0, 0, 0, 77, 88, 99];
        await File.WriteAllBytesAsync(path, old);
        var keys = NoKeyAccess();
        var store = scope.CreateStore(keys);
        Assert.Equal("failure_history_incompatible_draft",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadIndexAsync())).Code);
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync());
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10));
        Assert.Equal(old, await File.ReadAllBytesAsync(path));
        Assert.False(File.Exists(scope.Files.HistoryPath));
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
        Assert.False(File.Exists(path));
        Assert.Empty(await store.ReadIndexAsync());
        Assert.Empty(keys.CreateRequests);
    }

    [Theory]
    [InlineData("ciphertext")]
    [InlineData("schema")]
    [InlineData("missing-key")]
    public async Task Clear_IsKeylessAndPreservesCounterLockAndUnrelatedFiles(string damage)
    {
        using var scope = new FailureHistoryTestScope();
        await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        var state = (await scope.State.ReadAsync(default))!;
        var bytes = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        bytes[damage == "schema" ? 8 : bytes.Length - 1] ^= 0x80;
        await File.WriteAllBytesAsync(scope.Files.HistoryPath, bytes);
        var lockPath = Path.Combine(scope.DirectoryPath, EncryptedFailureHistoryStore.LockFileName);
        await File.WriteAllTextAsync(lockPath, "same-lock-file");
        var unrelated = Path.Combine(scope.DirectoryPath, ".history-v2-not-ours.tmp");
        await File.WriteAllTextAsync(unrelated, "keep");
        var unrelatedDirectory = Path.Combine(scope.DirectoryPath, "unrelated");
        Directory.CreateDirectory(unrelatedDirectory);
        var obsoleteFiles = new[]
        {
            FailureHistoryKeyNames.LegacyFileName, FailureHistoryKeyNames.GetFileName(0),
            $".history-{Guid.NewGuid():N}.tmp", $".history-v2-{Guid.NewGuid():N}.tmp",
        };
        foreach (var name in obsoleteFiles)
            await File.WriteAllBytesAsync(Path.Combine(scope.DirectoryPath, name), [1, 2, 3]);
        var keys = NoKeyAccess();
        var store = scope.CreateStore(keys);
        await store.ClearAsync();
        Assert.Empty(await store.ReadIndexAsync());
        Assert.Empty(await store.ReadRecordsAsync());
        Assert.Empty(keys.CreateRequests);
        Assert.Equal("same-lock-file", await File.ReadAllTextAsync(lockPath));
        Assert.Equal("keep", await File.ReadAllTextAsync(unrelated));
        Assert.True(Directory.Exists(unrelatedDirectory));
        Assert.All(obsoleteFiles, name => Assert.False(File.Exists(Path.Combine(scope.DirectoryPath, name))));
        var reset = (await scope.State.ReadAsync(default))!;
        Assert.Equal(state.NextSequence, reset.NextSequence);
        Assert.True(reset.Generation > state.Generation);
        Assert.False(reset.ClearPending);
        var keyGeneration = new TestFailureHistoryKeys();
        var entry = await scope.CreateStore(keyGeneration).SaveAsync(CreateRecord(reset.Generation), 10);
        Assert.EndsWith("-0001", entry.Id);
        Assert.Equal(reset.Generation, Assert.Single(keyGeneration.Generations));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleCapture_IsRejectedAfterOffEvenAfterReenable(bool reenable)
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        var pending = CreateRecord(await store.ReadGenerationAsync());
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
        if (reenable)
            await scope.CreateConfiguration().SetConfigurationAsync("agent.recovery", "manual");
        Assert.Equal(reenable ? "failure_history_stale_generation" : "failure_history_disabled",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(pending, 10))).Code);
        Assert.Empty(await store.ReadIndexAsync());
        if (reenable)
            await store.SaveAsync(CreateRecord(await store.ReadGenerationAsync()), 10);
    }

    [Fact]
    public async Task ConfigWriteFailure_DoesNotClearOrAdvanceHistory()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        await store.SaveAsync(CreateRecord(), 10);
        var old = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        var state = (await scope.State.ReadAsync(default))!;
        var broken = scope.CreateConfiguration(new CallbackProvider(scope.Provider,
            (_, _) => throw new IOException("configuration write failed")));
        await Assert.ThrowsAsync<IOException>(() => broken.SetConfigurationAsync("agent.recovery", "off"));
        Assert.Equal("manual", scope.Configuration.GetConfiguration<AgentOptions>().Recovery.Trigger);
        Assert.Equal(old, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
        Assert.Equal(state, await scope.State.ReadAsync(default));
    }

    [Fact]
    public async Task ControlWriteFailure_AfterOffCommitIsTruthfulAndReenableFencesOldWriters()
    {
        var fail = false;
        using var scope = new FailureHistoryTestScope(replace: (source, target) =>
        {
            if (fail && Path.GetFileName(target) == EncryptedFailureHistoryStore.ControlFileName)
                throw new IOException("cannot persist control");
            File.Move(source, target, overwrite: true);
        });
        var store = scope.CreateStore();
        await store.SaveAsync(CreateRecord(), 10);
        var pending = CreateRecord(await store.ReadGenerationAsync());
        fail = true;
        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => scope.Configuration.SetConfigurationAsync("agent.recovery", "off"));
        Assert.Contains("Recovery is off", error.Message);
        Assert.Equal("off", scope.Configuration.GetConfiguration<AgentOptions>().Recovery.Trigger);
        Assert.False((await scope.State.ReadAsync(default))!.ClearPending);
        Assert.True(File.Exists(scope.Files.HistoryPath));
        Assert.Equal("failure_history_clear_pending",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadIndexAsync())).Code);
        fail = false;
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "manual");
        Assert.Empty(await store.ReadIndexAsync());
        Assert.Equal("failure_history_stale_generation",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(pending, 10))).Code);
        await store.SaveAsync(CreateRecord(await store.ReadGenerationAsync()), 10);
    }

    [Fact]
    public async Task PartialDeleteFailure_KeepsOffAndPendingAndRetriesIdempotently()
    {
        var fail = false;
        using var scope = new FailureHistoryTestScope(delete: path =>
        {
            if (fail && Path.GetFileName(path) == EncryptedFailureHistoryStore.HistoryFileName)
                throw new IOException("cannot delete history");
            File.Delete(path);
        });
        await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        var keys = NoKeyAccess();
        var store = scope.CreateStore(keys);
        fail = true;
        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => scope.Configuration.SetConfigurationAsync("agent.recovery", "off"));
        Assert.Equal("failure_history_clear_incomplete", error.Code);
        Assert.Contains("Recovery is off", error.Message);
        var pending = (await scope.State.ReadAsync(default))!;
        Assert.True(pending.ClearPending);
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadIndexAsync());
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync());
        await Assert.ThrowsAsync<FailureHistoryException>(() => scope.Configuration.SetConfigurationAsync("agent.recovery", "manual"));
        Assert.Equal("off", scope.Configuration.GetConfiguration<AgentOptions>().Recovery.Trigger);
        fail = false;
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
        var completed = (await scope.State.ReadAsync(default))!;
        Assert.Equal(pending.Generation, completed.Generation);
        Assert.Equal(pending.NextSequence, completed.NextSequence);
        Assert.False(completed.ClearPending);
        Assert.Empty(await store.ReadIndexAsync());
        Assert.Empty(keys.CreateRequests);
    }

    [Fact]
    public async Task FailureCompletingTheClearMarker_DoesNotPretendDeletedDataIsEmptyHistory()
    {
        var fail = false;
        var controlWrites = 0;
        using var scope = new FailureHistoryTestScope(replace: (source, target) =>
        {
            if (fail && Path.GetFileName(target) == EncryptedFailureHistoryStore.ControlFileName && ++controlWrites == 2)
                throw new IOException("cannot complete cleanup marker");
            File.Move(source, target, overwrite: true);
        });
        var store = scope.CreateStore();
        await store.SaveAsync(CreateRecord(), 10);
        fail = true;
        await Assert.ThrowsAsync<FailureHistoryException>(() => scope.Configuration.SetConfigurationAsync("agent.recovery", "off"));
        Assert.False(File.Exists(scope.Files.HistoryPath));
        var pending = (await scope.State.ReadAsync(default))!;
        Assert.True(pending.ClearPending);
        Assert.Equal("failure_history_clear_pending",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadIndexAsync())).Code);
        fail = false;
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "off");
        Assert.Equal(pending.Generation, (await scope.State.ReadAsync(default))!.Generation);
        Assert.Empty(await store.ReadIndexAsync());
    }

    [Fact]
    public async Task CancellationAfterOffCommit_IsNotReportedAsAnUnchangedConfiguration()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        using var cancellation = new CancellationTokenSource();
        var config = scope.CreateConfiguration(new CallbackProvider(scope.Provider, async (document, token) =>
        {
            await scope.Provider.WriteAsync(document, token);
            cancellation.Cancel();
        }));
        var error = await Assert.ThrowsAsync<FailureHistoryException>(() =>
            config.SetConfigurationAsync("agent.recovery", "off", cancellationToken: cancellation.Token));
        Assert.Contains("Recovery is off", error.Message);
        Assert.Equal("off", scope.Configuration.GetConfiguration<AgentOptions>().Recovery.Trigger);
        await scope.Configuration.SetConfigurationAsync("agent.recovery", "manual");
        Assert.Empty(await scope.CreateStore().ReadIndexAsync());
    }

    [Fact]
    public async Task HandEditedOff_IsObservedOnlyByLifecycleNotReadonlyViews()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        await scope.Provider.WriteAsync(JsonNode.Parse("""{"agent":{"recovery":{"trigger":"off"}}}""")!.AsObject());
        var original = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        var store = scope.CreateStore(NoKeyAccess());
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadIndexAsync());
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync());
        Assert.Equal(original, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
        await store.ApplyPolicyAsync();
        Assert.Empty(await store.ReadIndexAsync());
        var sequence = (await scope.State.ReadAsync(default))!.NextSequence;
        await store.ApplyPolicyAsync();
        Assert.Equal(sequence, (await scope.State.ReadAsync(default))!.NextSequence);
    }

    [Fact]
    public async Task ObservedExternalOff_FencesInFlightCaptureEvenWithEmptyHistory()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore(NoKeyAccess());
        await store.ClearAsync();
        var pending = CreateRecord(await store.ReadGenerationAsync());
        await scope.Provider.WriteAsync(JsonNode.Parse("""{"agent":{"recovery":{"trigger":"off"}}}""")!.AsObject());
        await store.ApplyPolicyAsync();
        await scope.Provider.WriteAsync(JsonNode.Parse("""{"agent":{"recovery":{"trigger":"manual"}}}""")!.AsObject());
        Assert.Equal("failure_history_stale_generation",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(pending, 10))).Code);
        Assert.Empty(await store.ReadIndexAsync());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("unknown-version")]
    [InlineData("missing-field")]
    public async Task MissingOrCorruptAllocator_IsNeverResetFromRetainedRecords(string damage)
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        await store.SaveAsync(CreateRecord(), 10);
        if (damage == "missing")
            File.Delete(scope.Files.ControlPath);
        else
            await File.WriteAllTextAsync(scope.Files.ControlPath, damage switch
            {
                "malformed" => "{ broken",
                "unknown-version" => """{"version":99,"nextSequence":1,"generation":0,"clearPending":false}""",
                _ => """{"version":1,"generation":0,"clearPending":false}""",
            });
        var original = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        var expected = damage == "missing" ? "failure_history_control_missing" : "failure_history_control_corrupt";
        Assert.Equal(expected, (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadGenerationAsync())).Code);
        Assert.Equal(expected, (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ClearAsync())).Code);
        await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10));
        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => scope.Configuration.SetConfigurationAsync("agent.recovery", "off"));
        Assert.Contains("Recovery is off", error.Message);
        Assert.Equal(original, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
    }

    [Fact]
    public async Task StyleAndSizeMutations_NeverWaitForTheHistoryLock()
    {
        using var scope = new FailureHistoryTestScope();
        Directory.CreateDirectory(scope.DirectoryPath);
        using var held = new FileStream(Path.Combine(scope.DirectoryPath, EncryptedFailureHistoryStore.LockFileName),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await scope.Configuration.SetConfigurationAsync("agent.recovery.style", "interactive", cancellationToken: cancellation.Token);
        await scope.Configuration.SetConfigurationAsync("agent.recovery.historySize", 1, cancellationToken: cancellation.Token);
        Assert.Equal(1, scope.Configuration.GetConfiguration<AgentOptions>().Recovery.HistorySize);
    }

    private static TestFailureHistoryKeys NoKeyAccess() =>
        new() { Read = (_, _, _) => throw new InvalidOperationException("Reset and local index reads must never access keys.") };

    private sealed class CallbackProvider(IConfigurationProvider inner, Func<JsonObject, CancellationToken, Task> write) : IConfigurationProvider
    {
        public string GetConfigFilePath() => inner.GetConfigFilePath();
        public Task<JsonObject> ReadAsync(CancellationToken cancellationToken = default) => inner.ReadAsync(cancellationToken);
        public Task WriteAsync(JsonObject configuration, CancellationToken cancellationToken = default) => write(configuration, cancellationToken);
    }
}
