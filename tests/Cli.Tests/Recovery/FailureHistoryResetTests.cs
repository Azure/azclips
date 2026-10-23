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
    public async Task Clear_ResetsTheCounterWithTheFile()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        await store.SaveAsync(CreateRecord(), 10);
        await store.SaveAsync(CreateRecord(), 10);
        await store.ClearAsync();
        Assert.EndsWith("-0000", (await store.SaveAsync(CreateRecord(), 10)).Id);
    }

    [Fact]
    public async Task RetentionOne_AlternatingShellsNeverReuseIds()
    {
        using var scope = new FailureHistoryTestScope();
        var store = scope.CreateStore();
        var entries = new List<FailureHistoryEntry>();
        foreach (var pid in new uint[] { 2, 1, 2 })
            entries.Add(await store.SaveAsync(CreateRecord(pid: pid), 1));
        Assert.Equal(3, entries.Select(entry => entry.Id).Distinct().Count());
        Assert.Equal(entries[^1], Assert.Single(await store.ReadIndexAsync()));
    }

    [Fact]
    public async Task CounterWidthGrowth_KeepsIdsParseable()
    {
        using var scope = new FailureHistoryTestScope();
        Directory.CreateDirectory(scope.DirectoryPath);
        await File.WriteAllTextAsync(scope.Files.HistoryPath, """{"nextSequence":9999,"records":[]}""");
        var store = scope.CreateStore();
        Assert.EndsWith("-9999", (await store.SaveAsync(CreateRecord(), 10)).Id);
        Assert.EndsWith("-10000", (await store.SaveAsync(CreateRecord(), 10)).Id);
    }

    [Fact]
    public async Task Clear_IsKeylessAndDeletesOnlyHistoryTemporaryAndKeyFiles()
    {
        using var scope = new FailureHistoryTestScope();
        await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        var lockPath = Path.Combine(scope.DirectoryPath, EncryptedFailureHistoryStore.LockFileName);
        var unrelated = Path.Combine(scope.DirectoryPath, "history.json.not-ours.tmp");
        await File.WriteAllTextAsync(unrelated, "keep");
        var shortName = Path.Combine(scope.DirectoryPath, "history.json.tmp");
        await File.WriteAllTextAsync(shortName, "keep");
        var unrelatedDirectory = Path.Combine(scope.DirectoryPath, "unrelated");
        Directory.CreateDirectory(unrelatedDirectory);
        var controlled = new[] { FailureHistoryKeyNames.FileName, $"{EncryptedFailureHistoryStore.HistoryFileName}.4242.tmp" };
        foreach (var name in controlled)
            await File.WriteAllBytesAsync(Path.Combine(scope.DirectoryPath, name), [1, 2, 3]);
        var keys = NoKeyAccess();
        var store = scope.CreateStore(keys);

        await store.ClearAsync();

        Assert.Empty(await store.ReadIndexAsync());
        Assert.Empty(keys.CreateRequests);
        Assert.True(File.Exists(lockPath));
        Assert.Equal("keep", await File.ReadAllTextAsync(unrelated));
        Assert.Equal("keep", await File.ReadAllTextAsync(shortName));
        Assert.True(Directory.Exists(unrelatedDirectory));
        Assert.False(File.Exists(scope.Files.HistoryPath));
        Assert.All(controlled, name => Assert.False(File.Exists(Path.Combine(scope.DirectoryPath, name))));
    }

    [Fact]
    public async Task Clear_DeletesTheKeyFileLast()
    {
        var deleted = new List<string>();
        using var scope = new FailureHistoryTestScope(delete: path =>
        {
            deleted.Add(Path.GetFileName(path));
            File.Delete(path);
        });
        await scope.CreateStore().SaveAsync(CreateRecord(), 10);
        await File.WriteAllBytesAsync(Path.Combine(scope.DirectoryPath, FailureHistoryKeyNames.FileName), [1]);
        await File.WriteAllBytesAsync(Path.Combine(scope.DirectoryPath, $"{EncryptedFailureHistoryStore.HistoryFileName}.7.tmp"), [1]);

        await scope.CreateStore(NoKeyAccess()).ClearAsync();

        Assert.Equal(FailureHistoryKeyNames.FileName, deleted[^1]);
        Assert.Equal(3, deleted.Count);
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
        new() { Read = (_, _) => throw new InvalidOperationException("Reset and index reads must never access keys.") };
}
