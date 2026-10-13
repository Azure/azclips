using Core.Abstractions;
using Core.Authentication;
using Moq;
using Xunit;

namespace Cli.Tests;

/// <summary>Tests Azclips-owned managed identity credential behavior.</summary>
public sealed class ManagedIdentityCredentialTests
{
    private readonly AzureCredentialFactory _factory = new();
    private static readonly ILoggingService Logger = Mock.Of<ILoggingService>();
    private static readonly ITelemetryService Telemetry = Mock.Of<ITelemetryService>();

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

    [Fact]
    public void ScopeToResource_ShouldThrow_WhenMultipleScopesProvided()
    {
        var ex = Assert.Throws<UsageErrorException>(() =>
            ManagedIdentityCredential.ScopeToResource(new[]
            {
                "https://graph.microsoft.com/User.Read",
                "https://graph.microsoft.com/Mail.Read",
            }));
        Assert.Contains("single resource", ex.Message, System.StringComparison.Ordinal);
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
        // stub credential keeps this test off MSAL/IMDS
        var expected = new AccessToken("stub-token", DateTimeOffset.UtcNow.AddHours(1));

        var credential = new Mock<TokenCredential>();
        credential.Setup(c => c.GetTokenAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var factory = new Mock<AzureCredentialFactory>();
        factory.Setup(f => f.CreateManagedIdentityCredential(It.IsAny<string?>(), It.IsAny<ManagedIdentityIdType?>()))
            .Returns(credential.Object);

        var authenticator = new ManagedIdentityAuthenticator(
            factory.Object, Logger, Telemetry);

        var profile = new Profile
        {
            Name = "test",
            TenantId = "tenant-1",
            SubscriptionId = "sub-1",
            AuthMethod = AuthMethod.ManagedIdentity,
            ManagedIdentityId = ManagedIdentityIdCodec.Format(
                ManagedIdentityIdType.ClientId, "11111111-1111-1111-1111-111111111111"),
        };

        var result = await authenticator.AcquireTokenAsync(profile, scopes: null);

        Assert.Equal("stub-token", result.AccessToken);
        factory.Verify(f => f.CreateManagedIdentityCredential(It.IsAny<string?>(), It.IsAny<ManagedIdentityIdType?>()), Times.Once);
    }

    [Theory]
    [InlineData(ManagedIdentityIdType.ObjectId, "44444444-4444-4444-4444-444444444444", "44444444-4444-4444-4444-444444444444")]
    [InlineData(ManagedIdentityIdType.ClientId, "33333333-3333-3333-3333-333333333333", null)]
    [InlineData(ManagedIdentityIdType.ResourceId, "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id", null)]
    [InlineData(ManagedIdentityIdType.SystemAssigned, null, null)]
    public async Task ManagedIdentityAuthenticator_AuthenticateAsync_SetsEntraObjectIdOnlyForObjectIdSelector(
        ManagedIdentityIdType idType,
        string? idValue,
        string? expectedObjectId)
    {
        var credential = new Mock<TokenCredential>();
        credential.Setup(c => c.GetTokenAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessToken("stub-token", DateTimeOffset.UtcNow.AddHours(1)));

        var factory = new Mock<AzureCredentialFactory>();
        factory.Setup(f => f.CreateManagedIdentityCredential(It.IsAny<string?>(), It.IsAny<ManagedIdentityIdType?>()))
            .Returns(credential.Object);

        var authenticator = new ManagedIdentityAuthenticator(factory.Object, Logger, Telemetry);

        var request = idType switch
        {
            ManagedIdentityIdType.ObjectId => new LoginRequest { AuthMode = AuthMethod.ManagedIdentity, ManagedIdentityObjectId = idValue },
            ManagedIdentityIdType.ClientId => new LoginRequest { AuthMode = AuthMethod.ManagedIdentity, ManagedIdentityClientId = idValue },
            ManagedIdentityIdType.ResourceId => new LoginRequest { AuthMode = AuthMethod.ManagedIdentity, ManagedIdentityResourceId = idValue },
            _ => new LoginRequest { AuthMode = AuthMethod.ManagedIdentity },
        };

        var result = await authenticator.AuthenticateAsync(request);

        Assert.Equal(expectedObjectId, result.EntraObjectId);
    }

    [Theory]
    [InlineData(ManagedIdentityIdType.ObjectId, "44444444-4444-4444-4444-444444444444", "44444444-4444-4444-4444-444444444444")]
    [InlineData(ManagedIdentityIdType.ClientId, "33333333-3333-3333-3333-333333333333", null)]
    [InlineData(ManagedIdentityIdType.ResourceId, "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/id", null)]
    [InlineData(ManagedIdentityIdType.SystemAssigned, null, null)]
    public async Task ManagedIdentityAuthenticator_AcquireTokenAsync_SetsEntraObjectIdOnlyForObjectIdSelector(
        ManagedIdentityIdType idType,
        string? idValue,
        string? expectedObjectId)
    {
        var credential = new Mock<TokenCredential>();
        credential.Setup(c => c.GetTokenAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessToken("stub-token", DateTimeOffset.UtcNow.AddHours(1)));

        var factory = new Mock<AzureCredentialFactory>();
        factory.Setup(f => f.CreateManagedIdentityCredential(It.IsAny<string?>(), It.IsAny<ManagedIdentityIdType?>()))
            .Returns(credential.Object);

        var authenticator = new ManagedIdentityAuthenticator(factory.Object, Logger, Telemetry);
        var profile = new Profile
        {
            Name = "test",
            TenantId = "tenant-1",
            SubscriptionId = "sub-1",
            AuthMethod = AuthMethod.ManagedIdentity,
            ManagedIdentityId = ManagedIdentityIdCodec.Format(idType, idValue),
        };

        var result = await authenticator.AcquireTokenAsync(profile, scopes: null);

        Assert.Equal(expectedObjectId, result.EntraObjectId);
    }

    [Theory]
    [InlineData("not-a-jwt")]                                    // arbitrary token; contents are irrelevant
    [InlineData("eyJhbGciOiJub25lIn0.eyJhdWQiOiJhcm0ifQ.")]      // valid JWT, no 'tid' claim
    public async Task ManagedIdentityAuthenticator_DoesNotStoreTenantId(string token)
    {
        var expected = new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1));

        var credential = new Mock<TokenCredential>();
        credential.Setup(c => c.GetTokenAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var factory = new Mock<AzureCredentialFactory>();
        factory.Setup(f => f.CreateManagedIdentityCredential(It.IsAny<string?>(), It.IsAny<ManagedIdentityIdType?>()))
            .Returns(credential.Object);

        var authenticator = new ManagedIdentityAuthenticator(factory.Object, Logger, Telemetry);

        var request = new LoginRequest { AuthMode = AuthMethod.ManagedIdentity };
        var result = await authenticator.AuthenticateAsync(request);

        Assert.Equal(token, result.AccessToken);
        Assert.Equal(string.Empty, result.TenantId);
    }
}
