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
/// Logout must hand the whole profile to the auth service: service principal tokens live in an app cache
/// keyed by application, so a bare user principal name leaves the access token behind.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class LogoutProcessorTests
{
    private static LogoutProcessor CreateProcessor()
    {
        var processor = new LogoutProcessor();
        typeof(LoginProcessorBase)
            .GetField("Publisher", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(processor, Mock.Of<IEventPublisher>());
        return processor;
    }

    private static (StubAuthService Auth, StubProfileService Profiles) InitDi(Profile? activeProfile)
    {
        var auth = new StubAuthService();
        var profiles = new StubProfileService(seedDefault: false);
        if (activeProfile is not null)
        {
            profiles.SaveAndActivateProfileAsync(activeProfile).GetAwaiter().GetResult();
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

    private static Profile ServicePrincipalProfile() => new()
    {
        Name = "sp",
        TenantId = "22222222-2222-2222-2222-222222222222",
        SubscriptionId = "33333333-3333-3333-3333-333333333333",
        CloudName = "AzureCloud",
        AuthMethod = AuthMethod.Federated,
        ClientId = "11111111-1111-1111-1111-111111111111",
        UserPrincipalName = "11111111-1111-1111-1111-111111111111",
    };

    [Fact]
    public async Task ProcessAsync_ServicePrincipalProfile_PassesProfileNameToAuthService()
    {
        var profile = ServicePrincipalProfile();
        var (auth, profiles) = InitDi(profile);

        await CreateProcessor().ProcessAsync();

        Assert.True(auth.LogoutCalled);
        Assert.Equal(profile.Name, auth.LastLoggedOutProfileName);
        Assert.Null(await profiles.GetActiveProfileAsync());
    }

    [Fact]
    public async Task ProcessAsync_NoActiveProfile_DoesNotCallAuthService()
    {
        var (auth, _) = InitDi(activeProfile: null);

        await CreateProcessor().ProcessAsync();

        Assert.False(auth.LogoutCalled);
    }
}
