// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using Core.Abstractions;
using Core.Abstractions.Recovery;
using Core.Authentication;
using Core.Authentication.Recovery;
using Core.Runtime.Recovery;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cli.Tests.Recovery;

[Trait("Category", "NativeKeyStore")]
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

            var protectedBytes = await File.ReadAllBytesAsync(Path.Combine(_directory, FailureHistoryKeyNames.FileName));
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
    public async Task CorruptWindowsProtectedKey_FailsWithoutReplacementOrNativeDiagnostics()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, FailureHistoryKeyNames.FileName);
        byte[] invalidKey = [1, 2, 3, 4, 5];
        await File.WriteAllBytesAsync(path, invalidKey);

        var error = await Assert.ThrowsAsync<FailureHistoryException>(() =>
            new ProtectedFailureHistoryKeyStore(_directory).GetKeyAsync(createIfMissing: true));

        Assert.Equal("failure_history_key_unavailable", error.Code);
        Assert.NotNull(error.InnerException);
        Assert.Equal(invalidKey, await File.ReadAllBytesAsync(path));
    }

    [WindowsProtectedStoreFact]
    public async Task ReadOnlyEmptyWindowsKey_KeepsTheSwallowedWriteError()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, FailureHistoryKeyNames.FileName);
        await File.WriteAllBytesAsync(path, []);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            var debug = new List<string>();
            var logger = new Mock<ILoggingService>(MockBehavior.Strict);
            logger.Setup(log => log.Debug(It.IsAny<string>())).Callback<string>(debug.Add);

            var error = await Assert.ThrowsAsync<FailureHistoryException>(
                () => new ProtectedFailureHistoryKeyStore(_directory, logger.Object).GetKeyAsync(createIfMissing: true));

            Assert.Equal("failure_history_key_unavailable", error.Code);
            Assert.Contains("UnauthorizedAccessException", Assert.IsType<IOException>(error.InnerException).Message);
            // Written while MSAL retries, before the key call returns.
            Assert.Contains(debug, line => line.Contains("UnauthorizedAccessException", StringComparison.Ordinal));
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [WindowsProtectedStoreFact]
    public async Task InaccessibleWindowsKey_FailsWithoutRegeneration()
    {
        var store = new ProtectedFailureHistoryKeyStore(_directory);
        var key = await store.GetKeyAsync(createIfMissing: true);
        Clear(key);
        var path = Path.Combine(_directory, FailureHistoryKeyNames.FileName);
        var original = await File.ReadAllBytesAsync(path);
        using (var denyAccess = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var error = await Assert.ThrowsAsync<FailureHistoryException>(() => store.GetKeyAsync(createIfMissing: true));
            Assert.Equal("failure_history_key_unavailable", error.Code);
            Assert.IsAssignableFrom<IOException>(error.InnerException);
        }

        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    [WindowsProtectedStoreFact]
    public async Task LosingRealProtectedKey_SkipsOldRecordsAndTheNextSaveUsesANewKey()
    {
        var keys = new ProtectedFailureHistoryKeyStore(_directory);
        var store = new EncryptedFailureHistoryStore(keys, _directory);
        var record = new FailureRecord
        {
            TerminalSessionId = 48213,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Arguments = ["test", "isolated-protected-store"],
            FailedCommand = "az test isolated-protected-store",
            ErrorText = "Isolated test diagnostic.",
            ExitCode = 1,
        };
        await store.SaveAsync(record, 10);
        File.Delete(Path.Combine(_directory, FailureHistoryKeyNames.FileName));

        Assert.Empty(await store.ReadRecordsAsync());
        var entry = await store.SaveAsync(record, 10);

        Assert.Equal(entry.Id, Assert.Single(await store.ReadRecordsAsync()).Id);
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
        else if (Environment.GetEnvironmentVariable("AZCLIPS_RUN_NATIVE_KEY_STORE_TESTS") != "1")
        {
            Skip = "Native key-store integration checks require explicit opt-in in an approved environment; ordinary runs use fake protected backends.";
        }
    }
}
