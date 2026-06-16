using Core.Abstractions;
using Core.Authentication;
using Tests.Common.Stubs;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Unit tests for <see cref="ManagedIdentityCredential"/> and <see cref="AzureCredentialFactory"/>.
///
/// Managed-identity source detection (IMDS, App Service, Service Fabric, Cloud Shell, Azure Arc)
/// is owned by MSAL .NET's ManagedIdentityApplication, so Azclips no longer selects the source
/// itself. These tests therefore cover only the Azclips-owned behavior: credential invariant
/// validation, how a requested identity (system- or user-assigned by client/object/resource id)
/// maps onto the credential, the scope-to-resource conversion, and the factory seam that lets the
/// authenticator be tested with a stub credential. The real endpoint flow is exercised by
/// <see cref="ManagedIdentityE2ETests"/> against a mock IMDS server.
/// </summary>
public sealed class ManagedIdentityCredentialTests
{
    private readonly AzureCredentialFactory _factory = new();

    [Theory]
    [InlineData(null)]
    [InlineData(ManagedIdentityIdType.SystemAssigned)]
    public void Factory_ShouldCreateSystemAssigned_WhenNoIdAndSystemOrUnspecifiedType(ManagedIdentityIdType? idType)
    {
        var credential = Assert.IsType<ManagedIdentityCredential>(
            _factory.CreateManagedIdentityCredential(null, idType));

        Assert.Equal(ManagedIdentityIdType.SystemAssigned, credential.IdType);
        Assert.Null(credential.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ManagedIdentityIdType.SystemAssigned)]
    public void Factory_ShouldThrow_WhenSystemOrUnspecifiedTypeHasId(ManagedIdentityIdType? idType)
    {
        Assert.Throws<System.ArgumentException>(
            () => _factory.CreateManagedIdentityCredential("unexpected-id", idType));
    }

    [Theory]
    [InlineData(ManagedIdentityIdType.ClientId, "11111111-1111-1111-1111-111111111111")]
    [InlineData(ManagedIdentityIdType.ObjectId, "22222222-2222-2222-2222-222222222222")]
    [InlineData(ManagedIdentityIdType.ResourceId, "/subscriptions/s/resourceGroups/r/providers/Microsoft.ManagedIdentity/userAssignedIdentities/u")]
    public void Factory_ShouldPreserveUserAssignedIdAndType(ManagedIdentityIdType idType, string id)
    {
        var credential = Assert.IsType<ManagedIdentityCredential>(
            _factory.CreateManagedIdentityCredential(id, idType));

        Assert.Equal(idType, credential.IdType);
        Assert.Equal(id, credential.Id);
    }

    [Theory]
    [InlineData("https://management.azure.com/.default", "https://management.azure.com")]
    [InlineData("https://storage.azure.com/.default", "https://storage.azure.com")]
    [InlineData("https://vault.azure.net", "https://vault.azure.net")]
    public void ScopeToResource_ShouldStripDefaultSuffix(string scope, string expected)
    {
        Assert.Equal(expected, ManagedIdentityCredential.ScopeToResource(new[] { scope }));
    }

    [Fact]
    public void ScopeToResource_ShouldFallBackToArm_WhenNoScopesProvided()
    {
        Assert.Equal("https://management.core.windows.net/", ManagedIdentityCredential.ScopeToResource(System.Array.Empty<string>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Factory_ShouldThrow_WhenUserAssignedIdIsMissing(string? id)
    {
        Assert.Throws<System.ArgumentException>(
            () => _factory.CreateManagedIdentityCredential(id, ManagedIdentityIdType.ClientId));
    }

    [Fact]
    public async Task ManagedIdentityAuthenticator_HappyPath()
    {
        // Proves the TokenCredential seam: an overridden factory returns a stub credential
        // so the authenticator can be unit-tested without any real MSAL/IMDS call.
        var expected = new AccessToken("stub-token", DateTimeOffset.UtcNow.AddHours(1));
        var factory = new StubCredentialFactory(new StubCredential(expected));
        var authenticator = new ManagedIdentityAuthenticator(
            factory, new StubLoggingService(), new StubTelemetryService());

        var context = new AuthContext("tenant-1", "sub-1", LoginMode.ManagedIdentity)
        {
            ManagedIdentityId = AuthContext.FormatManagedIdentityId(
                ManagedIdentityIdType.ClientId, "11111111-1111-1111-1111-111111111111"),
        };

        var result = await authenticator.AcquireTokenAsync(context, scopes: null);

        Assert.Equal("stub-token", result.AccessToken);
        Assert.True(factory.WasCalled);
    }

    private sealed class StubCredentialFactory(TokenCredential credential) : AzureCredentialFactory
    {
        public bool WasCalled { get; private set; }

        public override TokenCredential CreateManagedIdentityCredential(
            string? identityId, ManagedIdentityIdType? idType)
        {
            WasCalled = true;
            return credential;
        }
    }

    private sealed class StubCredential(AccessToken token) : TokenCredential
    {
        public override Task<AccessToken> GetTokenAsync(string[] scopes, CancellationToken cancellationToken = default)
            => Task.FromResult(token);
    }
}
