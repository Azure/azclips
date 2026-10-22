// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Recovery;
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
        var store = scope.CreateStore();
        var entries = new List<FailureHistoryEntry>();
        foreach (var pid in new uint[] { 2, 1, 2 })
            entries.Add(await store.SaveAsync(CreateRecord(pid: pid), 1));
        await store.ClearAsync();
        entries.Add(await store.SaveAsync(CreateRecord(pid: 2), 1));
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

    [Theory]
    [InlineData("ciphertext")]
    [InlineData("schema")]
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
        var unrelated = Path.Combine(scope.DirectoryPath, ".history-not-ours.tmp");
        await File.WriteAllTextAsync(unrelated, "keep");
        var unrelatedDirectory = Path.Combine(scope.DirectoryPath, "unrelated");
        Directory.CreateDirectory(unrelatedDirectory);
        var controlled = new[] { FailureHistoryKeyNames.FileName, $".history-{Guid.NewGuid():N}.tmp" };
        foreach (var name in controlled)
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
        Assert.All(controlled, name => Assert.False(File.Exists(Path.Combine(scope.DirectoryPath, name))));
        Assert.Equal(state.NextSequence, (await scope.State.ReadAsync(default))!.NextSequence);
        var entry = await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        Assert.EndsWith("-0001", entry.Id);
    }

    [Fact]
    public async Task PartialDeleteFailure_ReportsIncompleteClearAndRetrySucceeds()
    {
        var fail = false;
        using var scope = new FailureHistoryTestScope(delete: path =>
        {
            if (fail && Path.GetFileName(path) == EncryptedFailureHistoryStore.HistoryFileName)
                throw new IOException("cannot delete history");
            File.Delete(path);
        });
        await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        var store = scope.CreateStore(NoKeyAccess());
        fail = true;
        Assert.Equal("failure_history_clear_incomplete",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ClearAsync())).Code);
        fail = false;
        await store.ClearAsync();
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
                "unknown-version" => """{"version":99,"nextSequence":1}""",
                _ => """{"version":1}""",
            });
        var original = await File.ReadAllBytesAsync(scope.Files.HistoryPath);
        var expected = damage == "missing" ? "failure_history_control_missing" : "failure_history_control_corrupt";
        Assert.Equal(expected, (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadIndexAsync())).Code);
        Assert.Equal(expected, (await Assert.ThrowsAsync<FailureHistoryException>(() => store.SaveAsync(CreateRecord(), 10))).Code);
        Assert.Equal(original, await File.ReadAllBytesAsync(scope.Files.HistoryPath));
    }

    [Theory]
    [InlineData("off", true)]
    [InlineData("OFF", true)]
    [InlineData(null, true)]
    [InlineData("manual", false)]
    [InlineData("auto", false)]
    public async Task ConfigurationChangeHandler_ClearsHistoryOnlyWhenTriggerBecomesOff(string? value, bool cleared)
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        await store.SaveAsync(CreateRecord(), 10);
        await new RecoveryConfigurationChangeHandler(store).OnChangedAsync(
            RecoveryConfigurationChangeHandler.TriggerKey, value, CancellationToken.None);
        Assert.Equal(cleared, (await store.ReadIndexAsync()).Count == 0);
    }

    private static TestFailureHistoryKeys NoKeyAccess() =>
        new() { Read = (_, _) => throw new InvalidOperationException("Reset and local index reads must never access keys.") };
}
