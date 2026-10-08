// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tests.Common.Stubs;

namespace Cli.Tests;

/// <summary>
/// Removing a profile must take its credentials with it, since tokens that outlive the profile naming
/// them are unreachable and sit on disk until they expire.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class ProfileRemoveProcessorTests
{
    private const string ClientA = "11111111-1111-1111-1111-111111111111";

    private static ProfileRemoveProcessor CreateProcessor(string name)
    {
        var processor = new ProfileRemoveProcessor { Name = name };
        typeof(ProfileProcessorBase)
            .GetField("Publisher", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(processor, Mock.Of<IEventPublisher>());
        return processor;
    }

    private static (StubAuthService Auth, StubProfileService Profiles) InitDi(params Profile[] seeded)
    {
        var auth = new StubAuthService();
        var profiles = new StubProfileService(seedDefault: false);
        foreach (var profile in seeded)
        {
            profiles.SaveProfileAsync(profile).GetAwaiter().GetResult();
        }

        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton(Mock.Of<ILoggingService>());
            services.AddSingleton<IAuthService>(auth);
            services.AddSingleton<IProfileService>(profiles);
        });
        AzclipsServiceProvider.Initialize(ClientType.CLI);
        return (auth, profiles);
    }

    private static Profile ServicePrincipalProfile(string name, string clientId) => new()
    {
        Name = name,
        TenantId = "33333333-3333-3333-3333-333333333333",
        AuthMethod = AuthMethod.Federated,
        ClientId = clientId,
        UserPrincipalName = clientId,
    };

    private static Profile UserProfile(string name, string upn) => new()
    {
        Name = name,
        TenantId = "33333333-3333-3333-3333-333333333333",
        AuthMethod = AuthMethod.Browser,
        UserPrincipalName = upn,
    };

    [Fact]
    public async Task ProcessAsync_ShouldDeleteTheDefinition_WithoutSigningTheUserOut()
    {
        var (auth, profiles) = InitDi(UserProfile("work", "user@contoso.com"));

        await CreateProcessor("work").ProcessAsync();

        Assert.Null(await profiles.GetProfileAsync("work"));
        Assert.False(auth.LogoutCalled, "Removing a profile must not end the session it named.");
    }

    [Fact]
    public async Task ProcessAsync_ShouldLeaveServicePrincipalTokensAlone()
    {
        // The app cache is shared, so it stays reachable for any other profile using the same application.
        var (auth, profiles) = InitDi(ServicePrincipalProfile("sp-a", ClientA));

        await CreateProcessor("sp-a").ProcessAsync();

        Assert.Null(await profiles.GetProfileAsync("sp-a"));
        Assert.False(auth.LogoutCalled);
    }

    [Fact]
    public async Task ProcessAsync_ShouldLeaveOtherProfilesIntact()
    {
        var (_, profiles) = InitDi(
            UserProfile("work", "user@contoso.com"),
            UserProfile("personal", "someone@contoso.com"));

        await CreateProcessor("work").ProcessAsync();

        Assert.Null(await profiles.GetProfileAsync("work"));
        Assert.NotNull(await profiles.GetProfileAsync("personal"));
    }

    [Fact]
    public async Task ProcessAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        var (auth, _) = InitDi();

        await Assert.ThrowsAsync<ArgumentException>(() => CreateProcessor("missing").ProcessAsync());
        Assert.False(auth.LogoutCalled);
    }
}
