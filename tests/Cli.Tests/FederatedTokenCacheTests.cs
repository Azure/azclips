// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Authentication;
using Moq;
using Tests.Common.Stubs;

namespace Cli.Tests;

/// <summary>
/// Every service principal shares one app cache, which holds no accounts, so logout can only evict by
/// emptying the whole file. Accepted trade: credentials remain in the secret store. Pinned to plaintext.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class FederatedTokenCacheTests : IDisposable
{
    private const string ClientA = "11111111-1111-1111-1111-111111111111";

    private readonly string _cacheDirectory = Directory.CreateTempSubdirectory("azclips-sp-cache").FullName;

    public FederatedTokenCacheTests() => MsalTokenCacheFactory.ForcePlaintextForTests = true;

    private MsalAuthService CreateAuthService(ILoggingService? logger = null, IProfileService? profiles = null)
    {
        logger ??= Mock.Of<ILoggingService>();
        var telemetry = Mock.Of<ITelemetryService>();
        var options = new MsalClientOptions { CacheDirectory = _cacheDirectory };
        var secretStore = new ServicePrincipalSecretStore();

        return new MsalAuthService(
            logger,
            telemetry,
            profiles ?? Mock.Of<IProfileService>(),
            new ClientMetadata(ClientType.CLI, ClientA),
            new ManagedIdentityAuthenticator(new AzureCredentialFactory(), logger, telemetry),
            new FederatedTokenAuthenticator(logger, telemetry, secretStore, options),
            new ServicePrincipalSecretAuthenticator(logger, telemetry, secretStore, options),
            secretStore,
            options);
    }

    /// <summary>Logout resolves the record by name, so the profile has to be in the store to be found.</summary>
    private async Task LogoutAsync(Profile profile)
    {
        var profiles = new StubProfileService(seedDefault: false);
        await profiles.SaveProfileAsync(profile);

        await CreateAuthService(profiles: profiles).LogoutAsync(profile.Name);
    }

    private string SeedCacheFile()
    {
        var path = MsalTokenCacheFactory.GetCacheFilePath(
            plaintextUnsafe: true,
            prefix: MsalTokenCacheFactory.AppCachePrefix,
            cacheDirectory: _cacheDirectory);

        File.WriteAllText(path, "cached-tokens");
        return path;
    }

    /// <summary>
    /// Logout empties the payload rather than removing the file, which is the only thing that works when
    /// the payload lives in a credential store. The surviving file is an empty cache, not a leftover.
    /// </summary>
    private static void AssertCacheHoldsNoTokens(string path)
    {
        Assert.True(File.Exists(path), "Emptying a cache leaves the file behind; only a clear removes it.");
        Assert.Equal("{}", File.ReadAllText(path));
    }

    private static Profile ServicePrincipalProfile(string name, string? clientId) => new()
    {
        Name = name,
        TenantId = "33333333-3333-3333-3333-333333333333",
        AuthMethod = AuthMethod.Federated,
        ClientId = clientId,
    };

    /// <summary>MSAL refuses a flat file shared between the app and user caches.</summary>
    [Fact]
    public void AppCachePrefix_ShouldDifferFromUserCachePrefix()
    {
        Assert.NotEqual(MsalTokenCacheFactory.AppCachePrefix, MsalTokenCacheFactory.UserCachePrefix);
    }

    [Fact]
    public void AppCachePrefix_ShouldKeepCacheFileInsideCacheDirectory()
    {
        var path = MsalTokenCacheFactory.GetCacheFilePath(
            plaintextUnsafe: true,
            prefix: MsalTokenCacheFactory.AppCachePrefix,
            cacheDirectory: _cacheDirectory);

        Assert.Equal(_cacheDirectory, Path.GetDirectoryName(Path.GetFullPath(path)));
    }

    /// <summary>
    /// Pins the accepted cost of one shared app cache: logout is not selective. The others re-acquire
    /// from their stored credential.
    /// </summary>
    [Fact]
    public async Task LogoutAsync_ShouldEvictEveryServicePrincipal()
    {
        var cache = SeedCacheFile();

        await LogoutAsync(ServicePrincipalProfile("sp-a", ClientA));

        AssertCacheHoldsNoTokens(cache);
    }

    /// <summary>
    /// With a single cache there is no partition to identify, so a missing client id no longer blocks
    /// eviction the way it did when the client id named the file.
    /// </summary>
    [Fact]
    public async Task LogoutAsync_ShouldStillEvict_WhenProfileHasNoClientId()
    {
        var cache = SeedCacheFile();

        await LogoutAsync(ServicePrincipalProfile("sp-unknown", clientId: null));

        AssertCacheHoldsNoTokens(cache);
    }

    [Fact]
    public async Task LogoutAsync_ShouldLeaveTheOtherLayoutAlone()
    {
        SeedCacheFile();

        // What an encrypted run leaves behind; this cache never wrote to the credential store, so logout
        // has no business reaching into it.
        var signal = MsalTokenCacheFactory.GetCacheFilePath(
            plaintextUnsafe: false,
            prefix: MsalTokenCacheFactory.AppCachePrefix,
            cacheDirectory: _cacheDirectory);
        File.WriteAllText(signal, string.Empty);

        await LogoutAsync(ServicePrincipalProfile("sp-a", ClientA));

        Assert.True(File.Exists(signal), "Clearing an unprotected cache must not touch the protected layout.");
    }

    [Fact]
    public async Task CreateAsync_ShouldLeaveTheOrphanedPlaintextCache_WhenTheCredentialStoreIsUsed()
    {
        var plaintext = SeedCacheFile();
        var prefix = MsalTokenCacheFactory.AppCachePrefix;

        // Resolution runs on every token refresh, so reclaiming the orphaned plaintext file belongs to an
        // explicit clear. The one test that wants real resolution, hence dropping the forced-plaintext seam.
        MsalTokenCacheFactory.ForcePlaintextForTests = false;
        var (_, plaintextUnsafe) = await MsalTokenCacheFactory.CreateAsync(prefix, _cacheDirectory);

        if (plaintextUnsafe)
        {
            // No usable credential store here, so plaintext was the right answer.
            return;
        }

        Assert.True(File.Exists(plaintext), "Resolving a protected cache must not delete anything.");

        await MsalTokenCacheFactory.RemoveAppCacheAsync(prefix, _cacheDirectory);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        MsalTokenCacheFactory.ForcePlaintextForTests = false;
        try
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp directory must not fail the run.
        }
    }
}
