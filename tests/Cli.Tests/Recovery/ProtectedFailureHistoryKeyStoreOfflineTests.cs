// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using Core.Abstractions.Recovery;
using Core.Authentication.Recovery;

namespace Cli.Tests.Recovery;

public sealed class ProtectedFailureHistoryKeyStoreOfflineTests
{
    [Fact]
    public async Task FakeProtectedBackend_RoundTripsOwnedCopiesAndUsesNewGenerationNamespaces()
    {
        using var scope = new FailureHistoryTestScope();
        var generations = new Dictionary<ulong, FakeStorage>();
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, generation =>
        {
            if (!generations.TryGetValue(generation, out var storage))
                generations[generation] = storage = new FakeStorage();
            return storage;
        });
        var first = await store.GetKeyAsync(0, true);
        var again = await store.GetKeyAsync(0, false);
        var afterReset = await store.GetKeyAsync(1, true);
        try
        {
            Assert.Equal(first, again);
            Assert.NotSame(first, again);
            Assert.False(first.AsSpan().SequenceEqual(afterReset));
            Assert.Equal(1, generations[0].Writes);
            Assert.Equal(1, generations[1].Writes);
            Assert.Equal("master-key.v2.0.protected", ProtectedFailureHistoryKeyStore.GetKeyFileName(0));
            Assert.NotEqual(ProtectedFailureHistoryKeyStore.GetKeyFileName(0), ProtectedFailureHistoryKeyStore.GetKeyFileName(1));
            Assert.Empty(Directory.GetFiles(scope.DirectoryPath));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(again);
            CryptographicOperations.ZeroMemory(afterReset);
            foreach (var storage in generations.Values)
                CryptographicOperations.ZeroMemory(storage.Bytes);
        }
    }

    [Fact]
    public async Task MissingReadAndCorruptMarker_NeverWriteOrRegenerate()
    {
        using var scope = new FailureHistoryTestScope();
        var backend = new FakeStorage();
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, _ => backend);
        Assert.Equal("failure_history_key_missing",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(0, false))).Code);
        Assert.False(Directory.Exists(scope.DirectoryPath));
        Directory.CreateDirectory(scope.DirectoryPath);
        var marker = Path.Combine(scope.DirectoryPath, ProtectedFailureHistoryKeyStore.GetKeyFileName(0));
        await File.WriteAllBytesAsync(marker, [1, 2, 3]);
        Assert.Equal("failure_history_key_missing",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(0, true))).Code);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(marker));
        Assert.Equal(0, backend.Writes);
    }

    [Fact]
    public async Task SwallowedPersistenceFailure_IsDetectedByReadBack()
    {
        using var scope = new FailureHistoryTestScope();
        var backend = new FakeStorage { DropWrites = true };
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, _ => backend);
        Assert.Equal("failure_history_key_unavailable",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(0, true))).Code);
        Assert.Equal(1, backend.Writes);
    }

    [Fact]
    public async Task NativeKeyMarker_IsNonsecretAndPreventsRegenerationAfterKeyLoss()
    {
        using var scope = new FailureHistoryTestScope();
        var backend = new FakeStorage { RequiresMarker = true };
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, _ => backend);
        var key = await store.GetKeyAsync(0, true);
        try
        {
            var marker = Path.Combine(scope.DirectoryPath, ProtectedFailureHistoryKeyStore.GetKeyFileName(0));
            Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(marker));
            backend.Clear();
            var error = await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(0, true));
            Assert.Equal("failure_history_key_missing", error.Code);
            Assert.Equal(1, backend.Writes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public async Task ReadOnlyNativeKey_DoesNotCreateMarkerButNextWriteEnabledReadDoes()
    {
        using var scope = new FailureHistoryTestScope();
        var backend = new FakeStorage { RequiresMarker = true };
        backend.WriteData(new byte[32]);
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, _ => backend);
        var readOnly = await store.GetKeyAsync(0, false);
        var marker = Path.Combine(scope.DirectoryPath, ProtectedFailureHistoryKeyStore.GetKeyFileName(0));
        Assert.False(Directory.Exists(scope.DirectoryPath));
        var writable = await store.GetKeyAsync(0, true);
        try
        {
            Assert.Equal(readOnly, writable);
            Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(marker));
            Assert.Equal(1, backend.Writes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(readOnly);
            CryptographicOperations.ZeroMemory(writable);
            backend.Clear();
        }
    }

    [Fact]
    public async Task UnconfirmedNativeWrite_DoesNotCreateMarker()
    {
        using var scope = new FailureHistoryTestScope();
        var backend = new FakeStorage { RequiresMarker = true, DropWrites = true };
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, _ => backend);
        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(0, true));
        Assert.Equal("failure_history_key_unavailable", error.Code);
        Assert.False(File.Exists(Path.Combine(scope.DirectoryPath, ProtectedFailureHistoryKeyStore.GetKeyFileName(0))));
    }

    [Fact]
    public void UnprovenNativeNoPromptSupport_FailsBeforeAnyNativeCall()
    {
        var error = Assert.Throws<FailureHistoryException>(() => ProtectedFailureHistoryKeyStore.EnsureNonInteractiveBackend(isWindows: false));
        Assert.Equal("failure_history_key_unavailable", error.Code);
        Assert.Contains("No native key access was attempted", error.Message);
        ProtectedFailureHistoryKeyStore.EnsureNonInteractiveBackend(isWindows: true);
    }

    [Fact]
    public async Task PreCancelledRequest_NeverConstructsAProtectedBackend()
    {
        using var scope = new FailureHistoryTestScope();
        var called = false;
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, _ =>
        {
            called = true;
            return new FakeStorage();
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.GetKeyAsync(0, true, cancellation.Token));
        Assert.False(called);
        Assert.False(Directory.Exists(scope.DirectoryPath));
    }

    private sealed class FakeStorage : IProtectedHistoryKeyStorage
    {
        internal byte[] Bytes { get; private set; } = [];
        internal bool DropWrites { get; init; }
        internal int Writes { get; private set; }
        public bool RequiresMarker { get; init; }
        public byte[] ReadData() => Bytes.ToArray();
        public void WriteData(byte[] data)
        {
            Writes++;
            if (!DropWrites)
                Bytes = data.ToArray();
        }

        internal void Clear()
        {
            CryptographicOperations.ZeroMemory(Bytes);
            Bytes = [];
        }
    }
}
