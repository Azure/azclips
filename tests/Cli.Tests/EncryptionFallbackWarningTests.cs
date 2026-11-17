// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Core.Authentication;
using Core.Runtime.Persistence;
using Moq;

namespace Cli.Tests;

/// <summary>
/// The plaintext fallback is recorded where it happens but reported at sign-in: every token refresh
/// resolves the cache, so warning from there would fire on every command.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class EncryptionFallbackWarningTests : IDisposable
{
    private static readonly FieldInfo FallbackField = typeof(MsalTokenCacheFactory)
        .GetField("_encryptionFallback", BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly bool _original = (bool)FallbackField.GetValue(null)!;

    private static void SetFallback(bool value) => FallbackField.SetValue(null, value);

    [Fact]
    public void EncryptionFallback_ShouldKeepReportingTheFallback()
    {
        SetFallback(true);

        // Sticky: the machine still has no credential store, so every login must keep saying so.
        Assert.True(MsalTokenCacheFactory.EncryptionFallback);
        Assert.True(MsalTokenCacheFactory.EncryptionFallback);
    }

    [Fact]
    public void EncryptionFallback_ShouldStayQuiet_WhenNoCacheFellBack()
    {
        SetFallback(false);

        Assert.False(MsalTokenCacheFactory.EncryptionFallback);
    }

    [Fact]
    public async Task CreateAsync_ShouldNeverWarn_WhateverTheCredentialStoreDoes()
    {
        SetFallback(false);
        MsalTokenCacheFactory.ForcePlaintextForTests = false;
        var logger = new Mock<ILoggingService>(MockBehavior.Loose);
        var directory = Directory.CreateTempSubdirectory("azclips-fallback-warning").FullName;
        const string Prefix = "msal.cache.app.fallback-test";

        try
        {
            // Resolution runs on every token refresh, so it must never warn either way.
            await MsalTokenCacheFactory.CreateAsync(
                prefix: Prefix,
                persistence: new FilePersistenceStore(directory),
                logger: logger.Object);

            logger.Verify(l => l.Warn(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            await MsalTokenCacheFactory.RemoveAppCacheAsync(Prefix, new FilePersistenceStore(directory));
            Directory.Delete(directory, recursive: true);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        FallbackField.SetValue(null, _original);
    }
}
