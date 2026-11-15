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
    // AuthMethod.Broker is a defined enum value with no CreateFromProfile mapping, so it hits the fallback.
    // The secret store is strict, so any secret lookup before the error raises MockException instead.
    [Fact]
    public async Task CreateFromProfile_UnsupportedAuthMethod_ThrowsInvalidInput()
    {
        var secretStore = new Mock<IServicePrincipalSecretStore>(MockBehavior.Strict);
        var profile = new Profile
        {
            Name = "p",
            TenantId = "22222222-2222-2222-2222-222222222222",
            SubscriptionId = "11111111-1111-1111-1111-111111111111",
            CloudName = "AzureCloud",
            AuthMethod = AuthMethod.Broker,
        };

        var ex = await Assert.ThrowsAsync<InvalidInputException>(
            () => AuthRequestFactory.CreateFromProfile(profile, secretStore.Object));

        Assert.Equal("usage error: Unsupported authentication method 'Broker'.", ex.Message);
    }

    [Fact]
    public async Task CreateFromProfile_ClientSecretMissingFromStore_ThrowsInvalidInput()
    {
        var secretStore = new Mock<IServicePrincipalSecretStore>(MockBehavior.Strict);
        secretStore.Setup(s => s.GetClientSecretAsync("11111111-1111-1111-1111-111111111111")).ReturnsAsync((string?)null);
        var profile = new Profile
        {
            Name = "p",
            TenantId = "22222222-2222-2222-2222-222222222222",
            AuthMethod = AuthMethod.ServicePrincipalSecret,
            ClientId = "11111111-1111-1111-1111-111111111111",
        };

        var ex = await Assert.ThrowsAsync<InvalidInputException>(
            () => AuthRequestFactory.CreateFromProfile(profile, secretStore.Object));

        Assert.Equal(
            "Could not load the persisted client secret for application '11111111-1111-1111-1111-111111111111'. Please sign in again.",
            ex.Message);
    }

    [Fact]
    public async Task CreateFromProfile_FederatedAssertionMissingFromStore_ThrowsInvalidInput()
    {
        var secretStore = new Mock<IServicePrincipalSecretStore>(MockBehavior.Strict);
        secretStore.Setup(s => s.GetAssertionAsync("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222"))
            .ReturnsAsync((string?)null);
        var profile = new Profile
        {
            Name = "p",
            TenantId = "22222222-2222-2222-2222-222222222222",
            AuthMethod = AuthMethod.Federated,
            ClientId = "11111111-1111-1111-1111-111111111111",
        };

        var ex = await Assert.ThrowsAsync<InvalidInputException>(
            () => AuthRequestFactory.CreateFromProfile(profile, secretStore.Object));

        Assert.Equal(
            "Could not load the persisted Federated Identity for application '11111111-1111-1111-1111-111111111111'. Please sign in again.",
            ex.Message);
    }
}
