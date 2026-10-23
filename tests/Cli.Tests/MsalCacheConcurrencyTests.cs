// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Authentication;
using Microsoft.Identity.Client.Extensions.Msal;
using Xunit;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public sealed class MsalCacheConcurrencyTests
{
    [Fact]
    public async Task ProtectedPersistenceVerificationWaitsForCacheLockOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;
        string directory = Path.Combine(Path.GetTempPath(), "azclips-cache-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var started = new ManualResetEventSlim();
        var properties = new StorageCreationPropertiesBuilder(
            MsalTokenCacheFactory.GetCacheFileName(false), directory).Build();
        var helper = await MsalCacheHelper.CreateAsync(properties);
        Task? creation = null;
        var cacheLock = new CrossPlatLock(
            MsalTokenCacheFactory.GetCacheFilePath(false, cacheDirectory: directory) + ".lockfile", 10, 500);
        try
        {
            creation = Task.Run(() =>
            {
                started.Set();
                using var verificationLock = MsalTokenCacheFactory.AcquirePersistenceVerificationLock(properties);
                helper.VerifyPersistence();
            });
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(250);
            Assert.False(creation.IsCompleted, "Protected-cache verification must respect the cross-process cache lock.");
            cacheLock.Dispose();
            await creation.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            cacheLock.Dispose();
            if (creation is not null) await creation.WaitAsync(TimeSpan.FromSeconds(10));
            Directory.Delete(directory, recursive: true);
        }

    }

    [Fact]
    public async Task ConcurrentProtectedCacheValidationDoesNotFallBackOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;
        string directory = Path.Combine(Path.GetTempPath(), "azclips-cache-race-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            for (int round = 0; round < 25; round++)
            {
                using var barrier = new Barrier(4);
                var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
                {
                    Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10)));
                    return await MsalTokenCacheFactory.CreateAsync(cacheDirectory: directory);
                })).ToArray();
                var results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
                Assert.All(results, result =>
                {
                    Assert.NotNull(result.Helper);
                    Assert.Equal(TokenCacheProtection.OsCredentialStore, result.Protection);
                });
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
