// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Authentication;
using Moq;

namespace Cli.Tests;

public class ServicePrincipalSecretAuthenticatorTests
{
    private const string ClientId = "aaaaaaaa-1111-1111-1111-111111111111";
    private const string TenantId = "bbbbbbbb-2222-2222-2222-222222222222";
    private const string Secret = "unit-test-secret";
    private readonly Mock<IServicePrincipalSecretStore> _secrets = new(MockBehavior.Strict);
    private readonly ILoggingService _logger = Mock.Of<ILoggingService>();
    private readonly ITelemetryService _telemetry = Mock.Of<ITelemetryService>();
    private readonly MsalClientOptions _options = new() { CacheDirectory = "unit-test-unused-cache-directory" };

    private ServicePrincipalSecretAuthenticator CreateAuthenticator() =>
        new(_logger, _telemetry, _secrets.Object, _options);

    public static IEnumerable<object[]> OtherCredentials()
    {
        yield return [new AuthCredential.Browser()];
        yield return [new AuthCredential.DeviceCode()];
        yield return [new AuthCredential.ManagedIdentity(ManagedIdentityIdType.SystemAssigned)];
        yield return [new AuthCredential.FederatedIdentity(ClientId, "unit-test-assertion")];
    }

    [Theory]
    [MemberData(nameof(OtherCredentials))]
    public async Task AuthenticateAsync_RejectsOtherCredentialTypes(AuthCredential credential)
    {
        var request = new LoginRequest(TenantId, null, credential);

        await Assert.ThrowsAsync<InvalidInputException>(() => CreateAuthenticator().AuthenticateAsync(request));

        _secrets.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("client ID", null)]
    [InlineData("client ID", "")]
    [InlineData("client ID", " ")]
    [InlineData("tenant ID", null)]
    [InlineData("tenant ID", "")]
    [InlineData("tenant ID", " ")]
    [InlineData("client secret", null)]
    [InlineData("client secret", "")]
    [InlineData("client secret", " ")]
    public async Task AuthenticateAsync_RequiresClientTenantAndSecret(string field, string? value)
    {
        var request = new LoginRequest(
            TenantId: field == "tenant ID" ? value : TenantId,
            Scopes: null,
            Credential: new AuthCredential.ClientSecret(
                field == "client ID" ? value! : ClientId,
                field == "client secret" ? value! : Secret));

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAuthenticator().AuthenticateAsync(request));

        Assert.Contains($"A {field} is required", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
        _secrets.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, " ")]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, " ")]
    public async Task AcquireTokenAsync_RequiresProfileClientAndTenant(bool invalidClient, string? value)
    {
        var profile = new Profile
        {
            Name = "automation",
            AuthMethod = AuthMethod.ServicePrincipalSecret,
            ClientId = invalidClient ? value : ClientId,
            TenantId = invalidClient ? TenantId : value!,
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAuthenticator().AcquireTokenAsync(profile, scopes: null));

        Assert.Contains(invalidClient ? "client ID" : "tenant ID", error.Message, StringComparison.Ordinal);
        _secrets.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AcquireTokenAsync_MissingStoredSecret_ReportsError(string? storedSecret)
    {
        _secrets.Setup(s => s.GetClientSecretAsync(ClientId, _options.CacheDirectory, _logger))
            .ReturnsAsync(storedSecret);
        var profile = new Profile
        {
            Name = "automation",
            AuthMethod = AuthMethod.ServicePrincipalSecret,
            ClientId = ClientId,
            TenantId = TenantId,
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateAuthenticator().AcquireTokenAsync(profile, scopes: null));

        Assert.Equal("Client secret not found in the secret store.", error.Message);
        _secrets.Verify(s => s.GetClientSecretAsync(ClientId, _options.CacheDirectory, _logger), Times.Once);
        _secrets.VerifyNoOtherCalls();
    }

    private MsalAuthService CreateAuthService(IProfileService profiles) => new(
        _logger, _telemetry, profiles, new ClientMetadata(ClientType.CLI, ClientId),
        new ManagedIdentityAuthenticator(new AzureCredentialFactory(), _logger, _telemetry),
        new FederatedTokenAuthenticator(_logger, _telemetry, _secrets.Object, _options),
        CreateAuthenticator(), _secrets.Object, _options);

    [Fact]
    public async Task LoginAsync_ClientSecret_RoutesToSecretAuthenticator()
    {
        var request = new LoginRequest(null, null, new AuthCredential.ClientSecret(ClientId, Secret));

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAuthService(Mock.Of<IProfileService>()).LoginAsync(request));

        Assert.Contains("tenant ID", error.Message, StringComparison.Ordinal);
        _secrets.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcquireTokenSilentlyAsync_ClientSecretProfile_RoutesToSecretStore(bool useActiveProfile)
    {
        var profile = new Profile
        {
            Name = "automation",
            AuthMethod = AuthMethod.ServicePrincipalSecret,
            ClientId = ClientId,
            TenantId = TenantId,
        };
        var profiles = new Mock<IProfileService>(MockBehavior.Strict);
        if (useActiveProfile)
        {
            profiles.Setup(p => p.GetActiveProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        }
        _secrets.Setup(s => s.GetClientSecretAsync(ClientId, _options.CacheDirectory, _logger))
            .ReturnsAsync((string?)null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateAuthService(profiles.Object).AcquireTokenSilentlyAsync(
                scopes: null, tenantId: null, profile: useActiveProfile ? null : profile));

        Assert.Equal("Client secret not found in the secret store.", error.Message);
        _secrets.Verify(s => s.GetClientSecretAsync(ClientId, _options.CacheDirectory, _logger), Times.Once);
        _secrets.VerifyNoOtherCalls();
        profiles.Verify(p => p.GetActiveProfileAsync(It.IsAny<CancellationToken>()),
            useActiveProfile ? Times.Once() : Times.Never());
        profiles.VerifyNoOtherCalls();
    }
}
