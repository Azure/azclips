// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts.Models;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public class LoginProcessorClientSecretTests
{
    private const string ClientId = "aaaaaaaa-1111-1111-1111-111111111111";
    private const string TenantId = "bbbbbbbb-2222-2222-2222-222222222222";
    private const string SubscriptionId = "cccccccc-3333-3333-3333-333333333333";
    private const string Secret = "unit-test-client-secret";

    private static LoginProcessor CreateLogin() => new()
    {
        ClientId = ClientId,
        ClientSecret = Secret,
        TenantId = TenantId,
        SubscriptionId = SubscriptionId,
    };

    private static void InitDi(
        ClientType surface,
        IAuthService? auth = null,
        IProfileService? profiles = null,
        ISubscriptionService? subscriptions = null,
        IServicePrincipalSecretStore? secrets = null)
    {
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton(Mock.Of<ILoggingService>());
            if (auth is not null) services.AddSingleton(auth);
            if (profiles is not null) services.AddSingleton(profiles);
            if (subscriptions is not null) services.AddSingleton(subscriptions);
            if (secrets is not null) services.AddSingleton(secrets);
        });
        AzclipsServiceProvider.Initialize(new ClientMetadata(surface, ClientId));
    }

    private static void Configure(LoginProcessor processor, Action<object>? onOutput = null)
    {
        var publisher = new Mock<IEventPublisher>();
        publisher.SetReturnsDefault(Task.CompletedTask);
        typeof(LoginProcessorBase).GetField("Publisher", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(processor, publisher.Object);
        typeof(LoginProcessorBase).GetField("WriteOutput", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(processor, (OutputStreamDelegate)((value, _) =>
            {
                onOutput?.Invoke(value);
                return Task.CompletedTask;
            }));
        LoginProcessorTestHelpers.ResolveParameterSet(processor);
    }

    [Theory]
    [InlineData(ClientType.CLI, null)]
    [InlineData(ClientType.CLI, false)]
    [InlineData(ClientType.CLI, true)]
    [InlineData(ClientType.PowerShell, null)]
    [InlineData(ClientType.PowerShell, false)]
    [InlineData(ClientType.PowerShell, true)]
    public void ResolveParameterSet_ClientSecret_DoesNotRequireServicePrincipalFlag(ClientType surface, bool? flag)
    {
        InitDi(surface);
        var processor = CreateLogin();
        processor.ServicePrincipal = flag;

        Assert.Equal("SPWithSecret", LoginProcessorTestHelpers.ResolveParameterSet(processor));
    }

    public static IEnumerable<object[]> InvalidArguments()
    {
        (string Property, object Value)[] cases =
        [
            (nameof(LoginProcessor.ClientId), ""),
            (nameof(LoginProcessor.ClientSecret), ""),
            (nameof(LoginProcessor.TenantId), ""),
            (nameof(LoginProcessor.SubscriptionId), ""),
            (nameof(LoginProcessor.FederatedToken), "header.payload.signature"),
            (nameof(LoginProcessor.Identity), true),
            (nameof(LoginProcessor.UseDeviceCode), true),
            (nameof(LoginProcessor.ObjectId), "dddddddd-4444-4444-4444-444444444444"),
            (nameof(LoginProcessor.ResourceId), "/subscriptions/s/resourceGroups/r"),
            (nameof(LoginProcessor.Profile), "existing"),
        ];
        foreach (var surface in new[] { ClientType.CLI, ClientType.PowerShell })
        {
            foreach (var (property, value) in cases)
            {
                yield return [surface, property, value];
            }
        }
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void ResolveParameterSet_IncompleteOrConflictingClientSecretArguments_Throws(
        ClientType surface, string property, object value)
    {
        InitDi(surface);
        var processor = CreateLogin();
        typeof(LoginProcessor).GetProperty(property)!.SetValue(processor, value);

        var error = Assert.Throws<InvalidInputException>(
            () => LoginProcessorTestHelpers.ResolveParameterSet(processor));

        var command = new ExtensionIndex().GetAllCommands(surface)
            .First(c => c.ProcessorType == typeof(LoginProcessor));
        var parameter = command.Parameters.Values.First(p => p.Name == property);
        var prefix = surface == ClientType.CLI ? "--" : "-";
        Assert.Contains(prefix + parameter.InvocationName, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI, false, null)]
    [InlineData(ClientType.CLI, false, "automation")]
    [InlineData(ClientType.CLI, true, null)]
    [InlineData(ClientType.PowerShell, false, null)]
    [InlineData(ClientType.PowerShell, false, "automation")]
    [InlineData(ClientType.PowerShell, true, null)]
    public async Task ProcessAsync_ClientSecret_SavesApplicationProfile(
        ClientType surface, bool reauthenticate, string? outProfile)
    {
        var existing = new Profile
        {
            Name = "existing",
            AuthMethod = AuthMethod.ServicePrincipalSecret,
            ClientId = ClientId,
            TenantId = TenantId,
            SubscriptionId = SubscriptionId,
            CloudName = "AzureCloud",
        };
        var profiles = new Mock<IProfileService>();
        profiles.Setup(p => p.GetProfileAsync(existing.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var secrets = new Mock<IServicePrincipalSecretStore>(MockBehavior.Strict);
        if (reauthenticate)
        {
            secrets.Setup(s => s.GetClientSecretAsync(ClientId, null, null)).ReturnsAsync(Secret);
        }

        var result = new AuthResult
        {
            AccessToken = "unit-test-access-token",
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            TenantId = TenantId,
            Scopes = ["https://management.azure.com/.default"],
            UserPrincipalName = ClientId,
        };
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        var subscriptions = new Mock<ISubscriptionService>();
        subscriptions.Setup(s => s.GetSubscriptionByIdAsync(
                result.AccessToken, SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionInfo(SubscriptionId, "Test", "Enabled"));
        InitDi(surface, auth.Object, profiles.Object, subscriptions.Object, secrets.Object);

        var processor = reauthenticate ? new LoginProcessor { Profile = existing.Name } : CreateLogin();
        processor.OutProfile = outProfile;
        var outputs = new List<object>();
        Configure(processor, outputs.Add);

        await processor.ProcessAsync();

        auth.Verify(a => a.LoginAsync(
            It.Is<LoginRequest>(r =>
                r.TenantId == TenantId && r.SubscriptionId == SubscriptionId &&
                r.Credential == new AuthCredential.ClientSecret(ClientId, Secret)),
            It.IsAny<CancellationToken>()), Times.Once);
        var expectedName = reauthenticate ? existing.Name : outProfile ?? ProfileNameValidator.ReservedDefaultName;
        var output = Assert.IsType<ProfileResult>(Assert.Single(outputs));
        Assert.Equal(expectedName, output.Name);
        Assert.Equal(AuthMethod.ServicePrincipalSecret, output.AuthMethod);
        Assert.Equal(ClientId, output.ClientId);
        Assert.True(output.IsActive);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(output), StringComparison.Ordinal);
        profiles.Verify(p => p.SaveAndActivateProfileAsync(
            It.Is<Profile>(p =>
                p.Name == expectedName && p.AuthMethod == AuthMethod.ServicePrincipalSecret &&
                p.ClientId == ClientId && p.UserPrincipalName == ClientId &&
                p.TenantId == TenantId && p.SubscriptionId == SubscriptionId &&
                p.CloudName == "AzureCloud" && p.ManagedIdentityId == null),
            It.IsAny<CancellationToken>()), Times.Once);
        subscriptions.Verify(s => s.GetSubscriptionByIdAsync(
            result.AccessToken, SubscriptionId, It.IsAny<CancellationToken>()), Times.Once);
        if (reauthenticate)
        {
            secrets.Verify(s => s.GetClientSecretAsync(ClientId, null, null), Times.Once);
        }
        secrets.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessAsync_ClientSecretFailure_DoesNotSaveProfile(bool subscriptionFails)
    {
        var profiles = new Mock<IProfileService>();
        var auth = new Mock<IAuthService>();
        var subscriptions = new Mock<ISubscriptionService>();
        if (subscriptionFails)
        {
            auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuthResult
                {
                    AccessToken = "unit-test-access-token",
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                    TenantId = TenantId,
                    Scopes = [],
                });
            subscriptions.Setup(s => s.GetSubscriptionByIdAsync(
                    It.IsAny<string>(), SubscriptionId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Forbidden"));
        }
        else
        {
            auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Rejected credential"));
        }
        InitDi(ClientType.CLI, auth.Object, profiles.Object, subscriptions.Object);
        var processor = CreateLogin();
        Configure(processor);

        if (subscriptionFails)
        {
            await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);
            subscriptions.VerifyNoOtherCalls();
        }
        profiles.Verify(p => p.SaveAndActivateProfileAsync(
            It.IsAny<Profile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tenant")]
    public void CreateClientSecret_PreservesCredentialAndScopes(string? tenant)
    {
        string[] scopes = ["https://graph.microsoft.com/.default"];

        var request = AuthRequestFactory.CreateClientSecret(ClientId, Secret, tenant, SubscriptionId, scopes);

        Assert.Equal(new AuthCredential.ClientSecret(ClientId, Secret), request.Credential);
        Assert.Equal(string.IsNullOrEmpty(tenant) ? null : tenant, request.TenantId);
        Assert.Equal(SubscriptionId, request.SubscriptionId);
        Assert.Equal(scopes, request.Scopes);
    }
}
