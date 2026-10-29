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
    public async Task MissingKey_IsReportedForReadsAndCreatedForWrites()
    {
        using var scope = new FailureHistoryTestScope();
        var backend = new FakeStorage();
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, () => backend);
        Assert.Equal("failure_history_key_missing",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(false))).Code);
        Assert.Equal(0, backend.Writes);
        var key = await store.GetKeyAsync(true);
        CryptographicOperations.ZeroMemory(key);
        Assert.Equal(1, backend.Writes);
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
    public async Task ConfirmedKeyLoss_CreatesANewKey()
    {
        using var scope = new FailureHistoryTestScope();
        var backend = new FakeStorage();
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, () => backend);
        var first = await store.GetKeyAsync(true);
        backend.Clear();
        var second = await store.GetKeyAsync(true);
        try
        {
            Assert.False(first.SequenceEqual(second));
            Assert.Equal(2, backend.Writes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(second);
            backend.Clear();
        }
    }

    [Fact]
    public async Task UnavailableStorage_NeverCreatesAKey()
    {
        using var scope = new FailureHistoryTestScope();
        var backend = new FakeStorage { Unavailable = true };
        var store = new ProtectedFailureHistoryKeyStore(scope.DirectoryPath, () => backend);
        Assert.Equal("failure_history_key_unavailable",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(true))).Code);
        Assert.Equal(0, backend.Writes);
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
        internal bool Unavailable { get; init; }
        public byte[] ReadData() => Unavailable
            ? throw new FailureHistoryException("failure_history_key_unavailable", "Locked.")
            : Bytes.ToArray();
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
