// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using Core.Abstractions.Recovery;
using Core.Authentication.Recovery;

namespace Cli.Tests.Recovery;

public sealed class ProtectedFailureHistoryKeyStoreOfflineTests
{
    [Fact]
    public async Task FakeProtectedBackend_RoundTripsOwnedCopies()
    {
        using var scope = new FailureHistoryTestScope();
        var storage = new FakeStorage();
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, () => storage);
        var first = await store.GetKeyAsync(true);
        var again = await store.GetKeyAsync(false);
        try
        {
            Assert.Equal(first, again);
            Assert.NotSame(first, again);
            Assert.Equal(1, storage.Writes);
            Assert.Equal("history.key", FailureHistoryKeyNames.FileName);
            Assert.Empty(Directory.GetFiles(scope.DirectoryPath));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(again);
            CryptographicOperations.ZeroMemory(storage.Bytes);
        }
    }

    [Fact]
    public async Task MissingReadAndCorruptMarker_NeverWriteOrRegenerate()
    {
        using var scope = new FailureHistoryTestScope();
        var backend = new FakeStorage();
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, () => backend);
        Assert.Equal("failure_history_key_missing",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(false))).Code);
        Assert.False(Directory.Exists(scope.DirectoryPath));
        Directory.CreateDirectory(scope.DirectoryPath);
        var marker = Path.Combine(scope.DirectoryPath, FailureHistoryKeyNames.FileName);
        await File.WriteAllBytesAsync(marker, [1, 2, 3]);
        Assert.Equal("failure_history_key_missing",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(true))).Code);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(marker));
        Assert.Equal(0, backend.Writes);
    }

    [Fact]
    public async Task SwallowedPersistenceFailure_IsDetectedByReadBack()
    {
        using var scope = new FailureHistoryTestScope();
        var backend = new FakeStorage { DropWrites = true };
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, () => backend);
        Assert.Equal("failure_history_key_unavailable",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(true))).Code);
        Assert.Equal(1, backend.Writes);
    }

    [Fact]
    public async Task NativeKeyMarker_IsNonsecretAndPreventsRegenerationAfterKeyLoss()
    {
        using var scope = new FailureHistoryTestScope();
        var backend = new FakeStorage { RequiresMarker = true };
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, () => backend);
        var key = await store.GetKeyAsync(true);
        try
        {
            var marker = Path.Combine(scope.DirectoryPath, FailureHistoryKeyNames.FileName);
            Assert.Equal(new byte[] { 1 }, await File.ReadAllBytesAsync(marker));
            backend.Clear();
            var error = await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(true));
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
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, () => backend);
        var readOnly = await store.GetKeyAsync(false);
        var marker = Path.Combine(scope.DirectoryPath, FailureHistoryKeyNames.FileName);
        Assert.False(Directory.Exists(scope.DirectoryPath));
        var writable = await store.GetKeyAsync(true);
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
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, () => backend);
        var error = await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(true));
        Assert.Equal("failure_history_key_unavailable", error.Code);
        Assert.False(File.Exists(Path.Combine(scope.DirectoryPath, FailureHistoryKeyNames.FileName)));
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
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, () =>
        {
            called = true;
            return new FakeStorage();
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.GetKeyAsync(true, cancellation.Token));
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
