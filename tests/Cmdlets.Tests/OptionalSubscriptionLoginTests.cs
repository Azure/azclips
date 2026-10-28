// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.ArgumentParsing;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts.Models;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public class OptionalSubscriptionLoginTests
{
    private const string TenantId = "11111111-1111-1111-1111-111111111111";
    private const string ClientId = "22222222-2222-2222-2222-222222222222";
    private const string SubscriptionId = "33333333-3333-3333-3333-333333333333";

    public static IEnumerable<object[]> LoginModes()
    {
        foreach (var surface in new[] { ClientType.CLI, ClientType.PowerShell })
        foreach (var mode in new[] { "Browser", "Tenant", "DeviceCode", "ManagedIdentity", "FederatedIdentity", "SPWithSecret" })
        foreach (var reauthenticate in new[] { false, true })
            yield return [surface, mode, reauthenticate];
    }

    [Theory]
    [MemberData(nameof(LoginModes))]
    public async Task LoginWithoutSubscription_SavesAndActivatesAuthenticatedTenant(
        ClientType surface, string mode, bool reauthenticate)
    {
        var processor = CreateLogin(mode);
        var existing = new Profile
        {
            Name = "tenant-only",
            TenantId = TenantId,
            AuthMethod = mode switch
            {
                "DeviceCode" => AuthMethod.DeviceCode,
                "ManagedIdentity" => AuthMethod.ManagedIdentity,
                "FederatedIdentity" => AuthMethod.Federated,
                "SPWithSecret" => AuthMethod.ServicePrincipalSecret,
                _ => AuthMethod.Browser
            },
            ClientId = ClientId
        };
        if (reauthenticate)
        {
            processor = new LoginProcessor
            {
                Profile = existing.Name,
                FederatedToken = mode == "FederatedIdentity" ? "test-federated-token" : null
            };
        }

        var profiles = new Mock<IProfileService>();
        profiles.Setup(p => p.GetProfileAsync(existing.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var subscriptions = new Mock<ISubscriptionService>(MockBehavior.Strict);
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResult
            {
                AccessToken = "test-token",
                TenantId = TenantId,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = [],
                CorrelationId = Guid.Empty
            });
        var secrets = new Mock<IServicePrincipalSecretStore>();
        secrets.Setup(s => s.GetClientSecretAsync(ClientId, null, null)).ReturnsAsync("test-secret");
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton(Mock.Of<ILoggingService>());
            services.AddSingleton(profiles.Object);
            services.AddSingleton(subscriptions.Object);
            services.AddSingleton(auth.Object);
            services.AddSingleton(secrets.Object);
        });
        AzclipsServiceProvider.Initialize(new ClientMetadata(surface, ClientId));
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        object? output = null;
        var outputHandler = new Mock<IOutputHandler>();
        outputHandler.SetupGet(h => h.WriteOutput).Returns((OutputStreamDelegate)((value, _) =>
        {
            output = value;
            return Task.CompletedTask;
        }));
        processor.Init(new ProcessorContext(
            Mock.Of<IRuntimeHttpPipeline>(), publisher.Object, Mock.Of<IAuthHandler>(), outputHandler.Object));

        await processor.ProcessAsync();

        profiles.Verify(p => p.SaveAndActivateProfileAsync(
            It.Is<Profile>(profile => profile.TenantId == TenantId
                && profile.SubscriptionId == null
                && profile.Name == (reauthenticate ? existing.Name : "default")),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(Assert.IsType<ProfileResult>(output).SubscriptionId);
        subscriptions.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PowerShellLogin_DoesNotInheritActiveSubscription(bool explicitSubscription)
    {
        var profiles = new Mock<IProfileService>();
        profiles.Setup(p => p.GetProfileKeysForDefault()).Returns(["SubscriptionId"]);
        profiles.Setup(p => p.GetProfileValueByKey(It.IsAny<Profile>(), "SubscriptionId"))
            .Returns(SubscriptionId);
        var metadata = new ExtensionIndex().GetCommandMetadata("Connect-AzAccount", ClientType.PowerShell);
        var processor = new LoginProcessor();
        var args = new Dictionary<string, object>();
        if (explicitSubscription) args["SubscriptionId"] = TenantId;

        new ArgumentParsingService(new PSArgumentParsingProvider(), profiles.Object)
            .BindArgumentsToProcessor(processor, args, metadata.Parameters, metadata.ParameterAliases,
                new Profile { SubscriptionId = SubscriptionId });

        Assert.Equal(explicitSubscription ? TenantId : null, processor.SubscriptionId);
    }

    private static LoginProcessor CreateLogin(string mode) => mode switch
    {
        "Tenant" => new() { TenantId = TenantId },
        "DeviceCode" => new() { UseDeviceCode = true },
        "ManagedIdentity" => new() { Identity = true },
        "FederatedIdentity" => new()
        {
            ServicePrincipal = true, ClientId = ClientId, TenantId = TenantId,
            FederatedToken = "test-federated-token"
        },
        "SPWithSecret" => new() { ClientId = ClientId, ClientSecret = "test-secret", TenantId = TenantId },
        _ => new()
    };
}
