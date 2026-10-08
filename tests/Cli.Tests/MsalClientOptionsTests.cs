// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Core.Abstractions;
using Core.Authentication;
using Microsoft.Identity.Client;
using Tests.Common.Stubs;
using Xunit;

namespace Cli.Tests;

// Guards the benchmark seam: MsalAuthService must honour the injected cache directory and
// MSAL HTTP factory, and production defaults must stay byte-for-byte what they were before.
// Shares the serialized collection with the other cache tests, so ForcePlaintextForTests is never
// observed by a test that did not set it.
[Collection("Azclips Service Provider Tests")]
public class MsalClientOptionsTests : IDisposable
{
    public MsalClientOptionsTests() => MsalTokenCacheFactory.ForcePlaintextForTests = true;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        MsalTokenCacheFactory.ForcePlaintextForTests = false;
    }

    private const string ClientId = "1950a258-227b-4e31-a9cf-717495945fc2";
    private const string TenantId = "00000000-0000-0000-0000-000000000000";
    private const string ObjectId = "11111111-1111-1111-1111-111111111111";
    private const string Environment = "login.windows.net";
    private const string Scope = "https://management.core.windows.net//.default";
    private const string Upn = "unit-test@contoso.com";
    private static string HomeAccountId => $"{ObjectId}.{TenantId}";

    private sealed class RecordingLogger : ILoggingService
    {
        public List<string> Messages { get; } = new();
        public void Log(string message) => Messages.Add(message);
        public void Info(string message) => Messages.Add(message);
        public void Verbose(string message) => Messages.Add(message);
        public void Debug(string message) => Messages.Add(message);
        public void Warn(string message) => Messages.Add(message);
        public void Error(string message) => Messages.Add(message);
    }

    private sealed class RecordingMsalHttpClientFactory : IMsalHttpClientFactory
    {
        public bool WasInvoked { get; private set; }

        public HttpClient GetHttpClient()
        {
            WasInvoked = true;
            return new HttpClient(new BlockingHandler());
        }

        private sealed class BlockingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken) =>
                throw new InvalidOperationException($"Blocked MSAL request to '{request.RequestUri}'.");
        }
    }

    private static MsalAuthService CreateService(
        MsalClientOptions options, ILoggingService logger, IProfileService profileService)
    {
        var telemetry = new StubTelemetryService();
        var managedIdentity = new ManagedIdentityAuthenticator(
            new AzureCredentialFactory(), logger, telemetry);
        var federated = new FederatedTokenAuthenticator(logger, telemetry, options);
        return new MsalAuthService(logger, telemetry, profileService, managedIdentity, federated, options);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "azclips-msal-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    // Minimal MSAL v3 cache document with a single account whose access token is already expired,
    // so a silent acquisition is forced to attempt a refresh over HTTP.
    private static void WriteExpiredCache(string directory)
    {
        var accessTokenKey =
            $"{HomeAccountId}-{Environment}-accesstoken-{ClientId}-{TenantId}-{Scope}".ToLowerInvariant();
        var refreshTokenKey =
            $"{HomeAccountId}-{Environment}-refreshtoken-{ClientId}--".ToLowerInvariant();
        var accountKey =
            $"{HomeAccountId}-{Environment}-{TenantId}".ToLowerInvariant();

        var json = $$"""
        {
          "AccessToken": {
            "{{accessTokenKey}}": {
              "credential_type": "AccessToken",
              "secret": "expired-access-token",
              "home_account_id": "{{HomeAccountId}}",
              "environment": "{{Environment}}",
              "client_id": "{{ClientId}}",
              "target": "{{Scope}}",
              "realm": "{{TenantId}}",
              "token_type": "Bearer",
              "cached_at": "1600000000",
              "expires_on": "1600003600",
              "extended_expires_on": "1600003600"
            }
          },
          "RefreshToken": {
            "{{refreshTokenKey}}": {
              "credential_type": "RefreshToken",
              "secret": "unit-test-refresh-token",
              "home_account_id": "{{HomeAccountId}}",
              "environment": "{{Environment}}",
              "client_id": "{{ClientId}}"
            }
          },
          "Account": {
            "{{accountKey}}": {
              "home_account_id": "{{HomeAccountId}}",
              "environment": "{{Environment}}",
              "realm": "{{TenantId}}",
              "local_account_id": "{{ObjectId}}",
              "username": "{{Upn}}",
              "authority_type": "MSSTS"
            }
          }
        }
        """;

        File.WriteAllText(Path.Combine(directory, UserCacheFileName), json, new UTF8Encoding(false));
    }

    /// <summary>What the factory resolves to once these tests force an unprotected cache.</summary>
    private static string UserCacheFileName => MsalTokenCacheFactory.GetCacheFileName(plaintextUnsafe: true);

    [Fact]
    public void CacheDirectory_ShouldDefaultToLocalAppDataRoot_WhenNotOverridden()
    {
        var options = new MsalClientOptions();

        Assert.Equal(HomePaths.GetDefaultLocalAppDataRoot(), options.CacheDirectory);
        Assert.Null(options.HttpClientFactory);
    }

    [Fact]
    public void GetCacheFileName_ShouldUseTheAzurePowerShellCasing()
    {
        // Azure PowerShell names its cache 'msal.cache' and Azure CLI 'msal_token_cache'; neither uses
        // upper case, so a user of all three sees one convention on disk and in the keyring.
        Assert.StartsWith("msal.cache.user", MsalTokenCacheFactory.GetCacheFileName(plaintextUnsafe: false), StringComparison.Ordinal);
        Assert.StartsWith("msal.cache.app", MsalTokenCacheFactory.AppCachePrefix, StringComparison.Ordinal);
    }

    [Fact]
    public void GetCacheFileName_ShouldPartitionByPrefixAndProtection()
    {
        // Credential stores key records by attributes rather than path, so the prefix has to reach those
        // too, and MSAL rejects a flat file shared between the app and user caches.
        Assert.NotEqual(
            MsalTokenCacheFactory.GetCacheFileName(plaintextUnsafe: false, MsalTokenCacheFactory.AppCachePrefix),
            MsalTokenCacheFactory.GetCacheFileName(plaintextUnsafe: false, MsalTokenCacheFactory.UserCachePrefix));

        Assert.NotEqual(
            MsalTokenCacheFactory.GetCacheFileName(plaintextUnsafe: false),
            MsalTokenCacheFactory.GetCacheFileName(plaintextUnsafe: true));
    }

    [Fact]
    public async Task AcquireTokenSilentlyAsync_ShouldUseConfiguredCacheDirectory_WhenOptionsOverrideIt()
    {
        var directory = CreateTempDirectory();
        var logger = new RecordingLogger();

        // Seed an expired cache so MSAL finds the account from that directory and then attempts
        // a network refresh. A blocking HTTP factory prevents real network traffic.
        // Empirically verified: WithUnprotectedFile() leaves the directory empty when no token is
        // written (MSAL does not create the cache file on registration). When the seeded file IS
        // present and read, MSAL reaches the network layer → InvalidOperationException. If MSAL
        // had used a different (empty) directory it would see no accounts and throw
        // MsalUiRequiredException("no_matching_account") instead — that distinction is the
        // behavioural proof that the configured directory was actually used.
        WriteExpiredCache(directory);

        var factory = new RecordingMsalHttpClientFactory();
        var options = new MsalClientOptions { CacheDirectory = directory, HttpClientFactory = factory };
        var service = CreateService(options, logger, new StubProfileService(userPrincipalName: Upn));

        // Behavioural assertion: InvalidOperationException (from the blocked refresh) means MSAL
        // found the cached account, proving it read from the configured directory.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AcquireTokenSilentlyAsync(scopes: null, tenantId: null));

        // Log assertion: the resolved path appeared in a debug message (weak but still useful evidence).
        var expected = Path.Combine(directory, UserCacheFileName);
        Assert.Contains(logger.Messages, m => m.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AcquireTokenSilentlyAsync_ShouldUseInjectedHttpClientFactory_WhenCachedTokenIsExpired()
    {
        var directory = CreateTempDirectory();
        WriteExpiredCache(directory);

        var factory = new RecordingMsalHttpClientFactory();
        var options = new MsalClientOptions { CacheDirectory = directory, HttpClientFactory = factory };
        var service = CreateService(options, new RecordingLogger(), new StubProfileService(userPrincipalName: Upn));

        // The refresh attempt fails because the injected handler blocks it — that failure is the proof.
        // Empirically verified: MSAL propagates the InvalidOperationException from BlockingHandler
        // directly (it does not wrap it), so this assertion proves the failure came from MSAL's
        // network layer rather than from an unrelated earlier error.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AcquireTokenSilentlyAsync(scopes: null, tenantId: null));

        Assert.True(factory.WasInvoked, "MSAL did not use the injected IMsalHttpClientFactory.");
    }

    [Fact]
    public async Task LogoutAsync_ShouldNotThrow_WhenTokenCacheAccessFails()
    {
        // Point the cache directory at a path that is actually a file so MSAL's token-cache
        // access fails. Removing an account from the cache is best-effort: the failure must not
        // prevent the caller (logout) from completing, so LogoutAsync should swallow it and warn.
        var fakeDirectory = Path.Combine(Path.GetTempPath(), "azclips-msal-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(fakeDirectory)!);
        File.WriteAllText(fakeDirectory, "this is a file, not a directory");

        try
        {
            var logger = new RecordingLogger();
            var options = new MsalClientOptions { CacheDirectory = fakeDirectory };
            var profiles = new StubProfileService(userPrincipalName: Upn);
            var service = CreateService(options, logger, profiles);

            // Must complete without throwing despite the invalid cache location.
            await service.LogoutAsync("default");

            // Assert on a stable phrase describing the non-fatal condition rather than exact wording,
            // and confirm the account UPN is not surfaced in the console-routed warning (PII).
            Assert.Contains(logger.Messages, m =>
                m.Contains("Non-fatal:", StringComparison.Ordinal) &&
                m.Contains("token-cache cleanup failed during logout", StringComparison.Ordinal) &&
                !m.Contains(Upn, StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(fakeDirectory))
            {
                File.Delete(fakeDirectory);
            }
        }
    }
}
