// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Authentication;
using Core.Runtime.Persistence;
using Moq;
using Tests.Common.Stubs;

namespace Cli.Tests;

/// <summary>
/// The service principal credential outlives the tokens minted from it, which is why it is stored apart
/// from the token cache. Pinned to plaintext so nothing reaches the machine's credential store.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class ServicePrincipalSecretStoreTests : IDisposable
{
    private const string ClientA = "11111111-1111-1111-1111-111111111111";
    private const string ClientB = "22222222-2222-2222-2222-222222222222";
    private const string TenantA = "33333333-3333-3333-3333-333333333333";
    private const string TenantB = "44444444-4444-4444-4444-444444444444";

    private readonly string _cacheDirectory = Directory.CreateTempSubdirectory("azclips-sp-secrets").FullName;
    private readonly IPersistenceStore _persistence;
    private readonly ServicePrincipalSecretStore _secretStore;

    public ServicePrincipalSecretStoreTests()
    {
        _persistence = new FilePersistenceStore(_cacheDirectory);
        _secretStore = new ServicePrincipalSecretStore(_persistence, new NullLoggingService());
        MsalTokenCacheFactory.ForcePlaintextForTests = true;
    }

    private Task SaveAsync(string clientId, string tenantId, string assertion) =>
        _secretStore.SaveAssertionAsync(clientId, tenantId, assertion);

    private Task<string?> GetAsync(string clientId, string tenantId) =>
        _secretStore.GetAssertionAsync(clientId, tenantId);

    [Fact]
    public void SecretStoreInterfaceDoesNotExposePersistence()
    {
        Assert.All(typeof(IServicePrincipalSecretStore).GetMethods(), method =>
            Assert.DoesNotContain(method.GetParameters(),
                parameter => parameter.ParameterType == typeof(IPersistenceStore)));
    }

    [Fact]
    public async Task GetClientSecret_ShouldReturnNull_WhenNothingWasStored()
    {
        Assert.Null(await _secretStore.GetClientSecretAsync(ClientA));
    }

    [Fact]
    public async Task SaveClientSecret_ShouldRoundTripAcrossStoreInstances()
    {
        await _secretStore.SaveClientSecretAsync(ClientA, "unit-test-secret");

        Assert.Equal("unit-test-secret",
            await new ServicePrincipalSecretStore(_persistence, new NullLoggingService()).GetClientSecretAsync(ClientA));
    }

    [Fact]
    public async Task SaveClientSecret_ShouldReplaceOnlyTheNamedApplication()
    {
        await _secretStore.SaveClientSecretAsync(ClientA, "first");
        await _secretStore.SaveClientSecretAsync(ClientB, "other");
        await _secretStore.SaveClientSecretAsync(ClientA, "replacement");

        Assert.Equal("replacement", await _secretStore.GetClientSecretAsync(ClientA));
        Assert.Equal("other", await _secretStore.GetClientSecretAsync(ClientB));
    }

    [Fact]
    public async Task GetClientSecret_ShouldIgnoreClientIdCase()
    {
        const string clientId = "abcdefab-1111-1111-1111-111111111111";
        await _secretStore.SaveClientSecretAsync(clientId, "unit-test-secret");

        Assert.Equal("unit-test-secret",
            await _secretStore.GetClientSecretAsync(clientId.ToUpperInvariant()));
    }

    [Fact]
    public async Task SaveClientSecret_ShouldPreserveOtherApplicationsAssertions()
    {
        await SaveAsync(ClientB, TenantA, "assertion");
        await _secretStore.SaveClientSecretAsync(ClientA, "unit-test-secret");

        Assert.Equal("assertion", await GetAsync(ClientB, TenantA));
        Assert.Null(await GetAsync(ClientA, TenantA));
        Assert.Null(await _secretStore.GetClientSecretAsync(ClientB));
    }

    [Fact]
    public async Task TryGetAssertion_ShouldReturnNull_WhenNothingWasStored()
    {
        Assert.Null(await GetAsync(ClientA, TenantA));
    }

    [Fact]
    public async Task SaveAssertion_ShouldRoundTrip()
    {
        await SaveAsync(ClientA, TenantA, "header.payload.signature");

        Assert.Equal("header.payload.signature", await GetAsync(ClientA, TenantA));
    }

    /// <summary>A second sign-in replaces the credential rather than accumulating one entry per login.</summary>
    [Fact]
    public async Task SaveAssertion_ShouldReplaceTheEarlierCredential()
    {
        await SaveAsync(ClientA, TenantA, "first");
        await SaveAsync(ClientA, TenantA, "second");

        Assert.Equal("second", await GetAsync(ClientA, TenantA));
    }

    /// <summary>One application in two tenants is two identities, so the client id alone is not a key.</summary>
    [Fact]
    public async Task SaveAssertion_ShouldKeepTenantsApart()
    {
        await SaveAsync(ClientA, TenantA, "for-tenant-a");
        await SaveAsync(ClientA, TenantB, "for-tenant-b");

        Assert.Equal("for-tenant-a", await GetAsync(ClientA, TenantA));
        Assert.Equal("for-tenant-b", await GetAsync(ClientA, TenantB));
    }

    /// <summary>Entra treats identifiers case-insensitively, so a lookup must too.</summary>
    [Fact]
    public async Task TryGetAssertion_ShouldIgnoreCase()
    {
        await SaveAsync(ClientA, TenantA, "stored");

        Assert.Equal("stored", await GetAsync(ClientA.ToUpperInvariant(), TenantA.ToUpperInvariant()));
    }

    [Fact]
    public async Task Remove_ShouldDropOnlyTheNamedApplication()
    {
        await SaveAsync(ClientA, TenantA, "a");
        await SaveAsync(ClientB, TenantA, "b");

        await _secretStore.RemoveAsync(ClientA, TenantA);

        Assert.Null(await GetAsync(ClientA, TenantA));
        Assert.Equal("b", await GetAsync(ClientB, TenantA));
    }

    /// <summary>Corruption must not break every command; it should read as nothing stored.</summary>
    [Fact]
    public async Task TryGetAssertion_ShouldIgnoreACorruptStore()
    {
        await SaveAsync(ClientA, TenantA, "a");
        File.WriteAllText(StorePath(), "not json");

        Assert.Null(await GetAsync(ClientA, TenantA));
    }

    /// <summary>Emptying only the token cache would let the next command sign straight back in.</summary>
    [Fact]
    public async Task LogoutAsync_ShouldForgetTheProfilesCredential()
    {
        await SaveAsync(ClientA, TenantA, "a");
        await SaveAsync(ClientB, TenantA, "b");

        var profile = new Profile
        {
            Name = "sp-a",
            TenantId = TenantA,
            AuthMethod = AuthMethod.Federated,
            ClientId = ClientA,
        };

        var profiles = new StubProfileService(seedDefault: false);
        await profiles.SaveProfileAsync(profile);

        await CreateAuthService(profiles).LogoutAsync(profile.Name);

        Assert.Null(await GetAsync(ClientA, TenantA));
        Assert.Equal("b", await GetAsync(ClientB, TenantA));
    }

    [Fact]
    public async Task Remove_ShouldDropClientSecretWithoutAStoredTenant()
    {
        await _secretStore.SaveClientSecretAsync(ClientA, "a");
        await _secretStore.SaveClientSecretAsync(ClientB, "b");
        await SaveAsync(ClientA, TenantB, "other-tenant-assertion");

        await _secretStore.RemoveAsync(ClientA, TenantA);

        Assert.Null(await _secretStore.GetClientSecretAsync(ClientA));
        Assert.Equal("b", await _secretStore.GetClientSecretAsync(ClientB));
        Assert.Equal("other-tenant-assertion", await GetAsync(ClientA, TenantB));
    }

    [Fact]
    public async Task LogoutAsync_ShouldForgetClientSecretAndEvictAppCache()
    {
        await _secretStore.SaveClientSecretAsync(ClientA, "a");
        await _secretStore.SaveClientSecretAsync(ClientB, "b");
        var appCachePath = MsalTokenCacheFactory.GetCacheFilePath(
            plaintextUnsafe: true, prefix: MsalTokenCacheFactory.AppCachePrefix,
            persistence: _persistence);
        File.WriteAllText(appCachePath, "cached-tokens");
        var profile = new Profile
        {
            Name = "secret-sp",
            TenantId = TenantA,
            AuthMethod = AuthMethod.ServicePrincipalSecret,
            ClientId = ClientA,
            UserPrincipalName = ClientA,
        };
        var profiles = new StubProfileService(seedDefault: false);
        await profiles.SaveProfileAsync(profile);

        await CreateAuthService(profiles).LogoutAsync(profile.Name);

        Assert.Null(await _secretStore.GetClientSecretAsync(ClientA));
        Assert.Equal("b", await _secretStore.GetClientSecretAsync(ClientB));
        Assert.Equal("{}", File.ReadAllText(appCachePath));
    }

    /// <summary>
    /// A name that resolves to nothing cannot identify a credential, but the caller still has to unset
    /// the active-profile pointer, so this stays non-fatal.
    /// </summary>
    [Fact]
    public async Task LogoutAsync_ShouldNotThrow_WhenTheProfileIsUnknown()
    {
        await SaveAsync(ClientA, TenantA, "a");

        await CreateAuthService(new StubProfileService(seedDefault: false)).LogoutAsync("gone");

        Assert.Equal("a", await GetAsync(ClientA, TenantA));
    }

    private string StorePath() => MsalTokenCacheFactory.GetCacheFilePath(
        plaintextUnsafe: true,
        prefix: ServicePrincipalSecretStore.Prefix,
        persistence: _persistence);

    private MsalAuthService CreateAuthService(IProfileService? profiles = null)
    {
        var logger = Mock.Of<ILoggingService>();
        var telemetry = Mock.Of<ITelemetryService>();
        var options = new MsalClientOptions(_persistence);

        return new MsalAuthService(
            logger,
            telemetry,
            profiles ?? Mock.Of<IProfileService>(),
            new ClientMetadata(ClientType.CLI, ClientA),
            new ManagedIdentityAuthenticator(new AzureCredentialFactory(), logger, telemetry),
            new FederatedTokenAuthenticator(logger, telemetry, _secretStore, options),
            new ServicePrincipalSecretAuthenticator(logger, telemetry, _secretStore, options),
            _secretStore,
            options);
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
