// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Moq;

namespace Extensions.Accounts.Tests;

/// <summary>
/// Unit tests for <see cref="AuthRequestFactory"/> error messages.
/// </summary>
public class AuthRequestFactoryTests
{
    private const string ClientId = "11111111-1111-1111-1111-111111111111";
    private const string TenantId = "22222222-2222-2222-2222-222222222222";

    // Strict: a secret lookup the test did not set up throws MockException instead of the expected error.
    private readonly Mock<IServicePrincipalSecretStore> _mockSecretStore = new(MockBehavior.Strict);

    // AuthMethod.Broker is a defined enum value with no CreateFromProfile mapping, so it hits the fallback.
    [Fact]
    public async Task CreateFromProfile_UnsupportedAuthMethod_ThrowsInvalidInput()
    {
        Profile profile = CreateProfile(authMethod: AuthMethod.Broker, clientId: null);

        Exception? exception = await Record.ExceptionAsync(() => AuthRequestFactory.CreateFromProfile(profile, _mockSecretStore.Object));

        Assert.Equal("usage error: Unsupported authentication method 'Broker'.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task CreateFromProfile_ClientSecretMissingFromStore_ThrowsInvalidInput()
    {
        _mockSecretStore.Setup(s => s.GetClientSecretAsync(ClientId)).ReturnsAsync((string?)null);
        Profile profile = CreateProfile(authMethod: AuthMethod.ServicePrincipalSecret, clientId: ClientId);

        Exception? exception = await Record.ExceptionAsync(() => AuthRequestFactory.CreateFromProfile(profile, _mockSecretStore.Object));

        Assert.Equal(
            $"Could not load the persisted client secret for application '{ClientId}'. Please sign in again.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task CreateFromProfile_FederatedAssertionMissingFromStore_ThrowsInvalidInput()
    {
        _mockSecretStore.Setup(s => s.GetAssertionAsync(ClientId, TenantId)).ReturnsAsync((string?)null);
        Profile profile = CreateProfile(authMethod: AuthMethod.Federated, clientId: ClientId);

        Exception? exception = await Record.ExceptionAsync(() => AuthRequestFactory.CreateFromProfile(profile, _mockSecretStore.Object));

        Assert.Equal(
            $"Could not load the persisted Federated Identity for application '{ClientId}'. Please sign in again.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    private Profile CreateProfile(AuthMethod authMethod, string? clientId) => new()
    {
        Name = "p",
        TenantId = TenantId,
        SubscriptionId = "33333333-3333-3333-3333-333333333333",
        CloudName = "AzureCloud",
        AuthMethod = authMethod,
        ClientId = clientId,
    };
}
