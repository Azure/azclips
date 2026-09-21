// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using Core.Abstractions.Recovery;
using Core.Authentication;
using Core.Authentication.Recovery;
using Core.Runtime.Recovery;
using Microsoft.Extensions.DependencyInjection;

namespace Cli.Tests.Recovery;

public sealed class ProtectedFailureHistoryKeyStoreTests : IDisposable
{
    private readonly string _directory = Path.GetFullPath(
        Path.Combine(".test-artifacts", "protected-failure-history", Guid.NewGuid().ToString("N")));

    [Fact]
    public void Constructor_DoesNotCreateState()
    {
        _ = new ProtectedFailureHistoryKeyStore(_directory);
        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void AuthenticationRegistration_ProvidesIndependentLazyKeyStore()
    {
        var services = new ServiceCollection();
        services.RegisterAuthenticationServices();
        using var provider = services.BuildServiceProvider();
        Assert.IsType<ProtectedFailureHistoryKeyStore>(provider.GetRequiredService<IFailureHistoryKeyStore>());
    }

    [Fact]
    public async Task CancelledKeyAccess_DoesNotCreateState()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ProtectedFailureHistoryKeyStore(_directory).GetKeyAsync(true, cancellation.Token));
        Assert.False(Directory.Exists(_directory));
    }

    [WindowsProtectedStoreFact]
    public async Task RealWindowsProtectedKey_RoundTripsOwnedCopies_InAnIsolatedDirectory()
    {
        var store = new ProtectedFailureHistoryKeyStore(_directory);
        byte[]? first = null;
        byte[]? second = null;
        byte[]? third = null;
        try
        {
            first = await store.GetKeyAsync(createIfMissing: true);
            second = await new ProtectedFailureHistoryKeyStore(Path.Combine(_directory, ".")).GetKeyAsync(createIfMissing: false);
            Assert.Equal(32, first.Length);
            Assert.NotSame(first, second);
            Assert.Equal(first, second);

            var protectedBytes = await File.ReadAllBytesAsync(Path.Combine(_directory, ProtectedFailureHistoryKeyStore.KeyFileName));
            Assert.True(protectedBytes.Length > 32);
            Assert.True(protectedBytes.AsSpan().IndexOf(first) < 0);
            Assert.Single(Directory.GetFiles(_directory));
            Assert.False(File.Exists(Path.Combine(_directory, "MSAL.cache")));

            CryptographicOperations.ZeroMemory(first);
            third = await store.GetKeyAsync(createIfMissing: false);
            Assert.Equal(second, third);
        }
        finally
        {
            Clear(first, second, third);
        }
    }

    [WindowsProtectedStoreFact]
    public async Task MissingWindowsKey_ReadDoesNotCreateDirectoryOrKey()
    {
        var error = await Assert.ThrowsAsync<FailureHistoryException>(() =>
            new ProtectedFailureHistoryKeyStore(_directory).GetKeyAsync(createIfMissing: false));
        Assert.Equal("failure_history_key_missing", error.Code);
        Assert.False(Directory.Exists(_directory));
    }

    [WindowsProtectedStoreFact]
    public async Task EmptyExistingWindowsProtectedKey_IsNotRegenerated()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, ProtectedFailureHistoryKeyStore.KeyFileName);
        await File.WriteAllBytesAsync(path, []);

        var error = await Assert.ThrowsAsync<FailureHistoryException>(() =>
            new ProtectedFailureHistoryKeyStore(_directory).GetKeyAsync(createIfMissing: true));

        Assert.Equal("failure_history_key_missing", error.Code);
        Assert.Empty(await File.ReadAllBytesAsync(path));
    }

    [WindowsProtectedStoreFact]
    public async Task CorruptWindowsProtectedKey_FailsWithoutReplacementOrNativeDiagnostics()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, ProtectedFailureHistoryKeyStore.KeyFileName);
        byte[] invalidKey = [1, 2, 3, 4, 5];
        await File.WriteAllBytesAsync(path, invalidKey);

        var error = await Assert.ThrowsAsync<FailureHistoryException>(() =>
            new ProtectedFailureHistoryKeyStore(_directory).GetKeyAsync(createIfMissing: true));

        Assert.Equal("failure_history_key_unavailable", error.Code);
        Assert.Null(error.InnerException);
        Assert.Equal(invalidKey, await File.ReadAllBytesAsync(path));
    }

    [WindowsProtectedStoreFact]
    public async Task InaccessibleWindowsKey_FailsWithoutRegeneration()
    {
        var store = new ProtectedFailureHistoryKeyStore(_directory);
        var key = await store.GetKeyAsync(createIfMissing: true);
        Clear(key);
        var path = Path.Combine(_directory, ProtectedFailureHistoryKeyStore.KeyFileName);
        var original = await File.ReadAllBytesAsync(path);
        using (var denyAccess = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var error = await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(createIfMissing: true));
            Assert.Equal("failure_history_key_unavailable", error.Code);
            Assert.Null(error.InnerException);
        }

        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [WindowsProtectedStoreFact]
    public async Task LosingRealProtectedKey_DoesNotResetOrOverwriteEncryptedHistory()
    {
        var keys = new ProtectedFailureHistoryKeyStore(_directory);
        var store = new EncryptedFailureHistoryStore(keys, _directory);
        var record = new FailureRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Arguments = ["test", "isolated-protected-store"],
            FailedCommand = "az test isolated-protected-store",
            ErrorText = "Isolated test diagnostic.",
            ExitCode = 1,
        };
        await store.SaveAsync(record, 10);
        var historyPath = Path.Combine(_directory, EncryptedFailureHistoryStore.HistoryFileName);
        var original = await File.ReadAllBytesAsync(historyPath);
        var keyPath = Path.Combine(_directory, ProtectedFailureHistoryKeyStore.KeyFileName);
        File.Delete(keyPath);

        Assert.Single(await store.ReadIndexAsync());
        Assert.Equal("failure_history_key_missing",
            (await Assert.ThrowsAsync<FailureHistoryException>(() => store.ReadRecordsAsync())).Code);
        Assert.Equal("failure_history_key_missing",
            (await Assert.ThrowsAsync<FailureHistoryException>(() =>
                store.SaveAsync(record with { Id = Guid.NewGuid().ToString("N") }, 10))).Code);
        Assert.False(File.Exists(keyPath));
        Assert.Equal(original, await File.ReadAllBytesAsync(historyPath));
    }

    [WindowsProtectedStoreFact]
    public async Task IndependentDirectories_UseIndependentProtectedKeys()
    {
        byte[]? first = null;
        byte[]? second = null;
        try
        {
            first = await new ProtectedFailureHistoryKeyStore(Path.Combine(_directory, "one")).GetKeyAsync(true);
            second = await new ProtectedFailureHistoryKeyStore(Path.Combine(_directory, "two")).GetKeyAsync(true);
            Assert.False(first.AsSpan().SequenceEqual(second));
        }
        finally
        {
            Clear(first, second);
        }
    }

    private static void Clear(params byte[]?[] keys)
    {
        foreach (var key in keys)
        {
            if (key is not null)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}

public sealed class WindowsProtectedStoreFactAttribute : FactAttribute
{
    public WindowsProtectedStoreFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "The isolated DPAPI integration check requires Windows; unit tests use an injected key provider.";
        }
    }
}
