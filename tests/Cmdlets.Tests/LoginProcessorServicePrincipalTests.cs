// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cmdlets.Tests;

/// <summary>
/// Validation for federated (OIDC) service principal sign-in. <c>ClientId</c> doubles as the managed
/// identity selector, so the rules gating one flow must not leak into the other.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class LoginProcessorServicePrincipalTests
{
    private static void InitDi(ClientType clientType)
    {
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
            services.AddSingleton(new Mock<ILoggingService>().Object));
        AzclipsServiceProvider.Initialize(
            new ClientMetadata(clientType, "00000000-0000-0000-0000-000000000000"));
    }

    private static void Validate(LoginProcessor processor)
    {
        LoginProcessorTestHelpers.ResolveParameterSet(processor);
    }

    private static string FlagFor(ClientType surface, string propertyName)
    {
        var meta = new ExtensionIndex().GetAllCommands(surface)
            .First(c => c.ProcessorType == typeof(LoginProcessor));
        var invocationName = meta.Parameters.Values
            .First(p => string.Equals(p.Name, propertyName, StringComparison.Ordinal))
            .InvocationName;
        return (surface == ClientType.PowerShell ? "-" : "--") + invocationName;
    }

    private static LoginProcessor ValidServicePrincipalLogin() => new()
    {
        ServicePrincipal = true,
        ClientId = "11111111-1111-1111-1111-111111111111",
        TenantId = "22222222-2222-2222-2222-222222222222",
        SubscriptionId = "33333333-3333-3333-3333-333333333333",
        FederatedToken = "header.payload.signature",
    };

    private static void InitializeProfileReauthDi(
    ClientType surface,
    Mock<IProfileService> profiles,
    Mock<IAuthService> auth,
    Mock<ISubscriptionService>? subscriptions = null)
    {
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton(new Mock<ILoggingService>().Object);
            services.AddSingleton(profiles.Object);
            services.AddSingleton(auth.Object);
            if (subscriptions is not null)
            {
                services.AddSingleton(subscriptions.Object);
            }
        });
        AzclipsServiceProvider.Initialize(
            new ClientMetadata(surface, "00000000-0000-0000-0000-000000000000"));
    }

    private static void ConfigureForDirectProcessAsync(LoginProcessor processor, bool setWriteOutput = false)
    {
        typeof(LoginProcessorBase)
            .GetField("Publisher", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(processor, new Mock<IEventPublisher>().Object);

        if (setWriteOutput)
        {
            typeof(LoginProcessorBase)
                .GetField("WriteOutput", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(processor, (OutputStreamDelegate)((_, _) => Task.CompletedTask));
        }

        // Init() wires up the parameter-set resolver; these tests drive ProcessAsync directly.
        typeof(LoginProcessorBase)
            .GetMethod("ConfigureParameterSets", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(processor, null);
    }

    [Fact]
    public void Initialize_RegistersClientMetadataAsTheSingleClientContract()
    {
        InitDi(ClientType.CLI);

        var metadata = AzclipsServiceProvider.GetRequiredService<ClientMetadata>();

        Assert.Equal(ClientType.CLI, metadata.ClientType);
        Assert.Throws<InvalidOperationException>(
            () => AzclipsServiceProvider.GetRequiredService<ClientType>());
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_ValidFederatedLogin_Succeeds(ClientType surface)
    {
        InitDi(surface);
        Validate(ValidServicePrincipalLogin());
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_ServicePrincipalWithoutFederatedToken_Throws(ClientType surface)
    {
        InitDi(surface);
        var processor = ValidServicePrincipalLogin();
        processor.FederatedToken = "";

        var ex = Assert.Throws<InvalidInputException>(() => Validate(processor));
        Assert.Contains(FlagFor(surface, nameof(LoginProcessor.FederatedToken)), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_ServicePrincipalWithoutClientId_Throws(ClientType surface)
    {
        InitDi(surface);
        var processor = ValidServicePrincipalLogin();
        processor.ClientId = "";

        var ex = Assert.Throws<InvalidInputException>(() => Validate(processor));
        Assert.Contains(FlagFor(surface, nameof(LoginProcessor.ClientId)), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_ServicePrincipalWithoutTenant_Throws(ClientType surface)
    {
        InitDi(surface);
        var processor = ValidServicePrincipalLogin();
        processor.TenantId = "";

        var ex = Assert.Throws<InvalidInputException>(() => Validate(processor));
        Assert.Contains(FlagFor(surface, nameof(LoginProcessor.TenantId)), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_ServicePrincipalWithIdentity_Throws(ClientType surface)
    {
        InitDi(surface);
        var processor = ValidServicePrincipalLogin();
        processor.Identity = true;

        var ex = Assert.Throws<InvalidInputException>(() => Validate(processor));
        Assert.Contains(FlagFor(surface, nameof(LoginProcessor.ServicePrincipal)), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_ServicePrincipalWithDeviceCode_Throws(ClientType surface)
    {
        InitDi(surface);
        var processor = ValidServicePrincipalLogin();
        processor.UseDeviceCode = true;

        var ex = Assert.Throws<InvalidInputException>(() => Validate(processor));
        Assert.Contains(FlagFor(surface, nameof(LoginProcessor.UseDeviceCode)), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_FederatedTokenWithoutServicePrincipal_Throws(ClientType surface)
    {
        InitDi(surface);
        var processor = ValidServicePrincipalLogin();
        processor.ServicePrincipal = false;
        processor.ClientId = "";

        var ex = Assert.Throws<InvalidInputException>(() => Validate(processor));
        Assert.Contains(FlagFor(surface, nameof(LoginProcessor.ServicePrincipal)), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_ServicePrincipalWithManagedIdentitySelector_Throws(ClientType surface)
    {
        InitDi(surface);
        var processor = ValidServicePrincipalLogin();
        processor.ResourceId = "/subscriptions/x/resourcegroups/y";

        var ex = Assert.Throws<InvalidInputException>(() => Validate(processor));
        Assert.Contains(FlagFor(surface, nameof(LoginProcessor.ResourceId)), ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The application ID travels in <c>ClientId</c>, which otherwise requires the managed identity flag,
    /// so that rule must not reject a legitimate federated login.
    /// </summary>
    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_ClientIdWithServicePrincipal_DoesNotRequireIdentityFlag(ClientType surface)
    {
        InitDi(surface);
        Validate(ValidServicePrincipalLogin());

        // Same ClientId without --service-principal is still rejected.
        var withoutSp = new LoginProcessor
        {
            ClientId = "11111111-1111-1111-1111-111111111111",
            TenantId = "22222222-2222-2222-2222-222222222222",
            SubscriptionId = "33333333-3333-3333-3333-333333333333",
        };
        Assert.Throws<InvalidInputException>(() => Validate(withoutSp));
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_ServicePrincipalWithProfileReauth_Throws(ClientType surface)
    {
        InitDi(surface);
        var processor = ValidServicePrincipalLogin();
        processor.Profile = "existing";

        var ex = Assert.Throws<InvalidInputException>(() => Validate(processor));
        Assert.Contains(FlagFor(surface, nameof(LoginProcessor.ServicePrincipal)), ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Re-auth takes the application, tenant and subscription from the profile and only a newly issued
    /// token from the caller.
    /// </summary>
    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void ValidateArguments_FederatedTokenWithProfileReauth_Succeeds(ClientType surface)
    {
        InitDi(surface);
        Validate(new LoginProcessor
        {
            Profile = "existing",
            FederatedToken = "header.payload.signature",
        });
    }

    /// <summary>The token is a credential, not a mode, so it is the one flag re-auth accepts.</summary>
    [Theory]
    [InlineData(ClientType.CLI, nameof(LoginProcessor.ClientId))]
    [InlineData(ClientType.CLI, nameof(LoginProcessor.Identity))]
    [InlineData(ClientType.CLI, nameof(LoginProcessor.UseDeviceCode))]
    [InlineData(ClientType.PowerShell, nameof(LoginProcessor.ClientId))]
    [InlineData(ClientType.PowerShell, nameof(LoginProcessor.Identity))]
    [InlineData(ClientType.PowerShell, nameof(LoginProcessor.UseDeviceCode))]
    public void ValidateArguments_ProfileReauthWithIdentityFlag_Throws(ClientType surface, string propertyName)
    {
        InitDi(surface);
        var processor = new LoginProcessor
        {
            Profile = "existing",
            FederatedToken = "header.payload.signature",
        };
        switch (propertyName)
        {
            case nameof(LoginProcessor.ClientId):
                processor.ClientId = "11111111-1111-1111-1111-111111111111";
                break;
            case nameof(LoginProcessor.Identity):
                processor.Identity = true;
                break;
            default:
                processor.UseDeviceCode = true;
                break;
        }

        var ex = Assert.Throws<InvalidInputException>(() => Validate(processor));
        Assert.Contains(FlagFor(surface, propertyName), ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Re-auth cannot enforce this in validation the way a fresh login does, because the stored auth
    /// method is known only after the profile is read.
    /// </summary>
    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public async Task ProcessAsync_ServicePrincipalProfileReauthWithoutToken_Throws(ClientType surface)
    {
        var profiles = new Mock<IProfileService>();
        profiles.Setup(p => p.GetProfileAsync("ci", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Profile
                {
                    Name = "ci",
                    TenantId = "22222222-2222-2222-2222-222222222222",
                    SubscriptionId = "33333333-3333-3333-3333-333333333333",
                    CloudName = "AzureCloud",
                    AuthMethod = AuthMethod.Federated,
                    ClientId = "11111111-1111-1111-1111-111111111111",
                });

        var auth = new Mock<IAuthService>();

        InitializeProfileReauthDi(surface, profiles, auth);

        var processor = new LoginProcessor { Profile = "ci" };
        ConfigureForDirectProcessAsync(processor);

        var ex = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);
        Assert.Contains(FlagFor(surface, nameof(LoginProcessor.FederatedToken)), ex.Message, StringComparison.Ordinal);

        // Nothing signed in, nothing persisted.
        auth.Verify(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        profiles.Verify(p => p.SaveAndActivateProfileAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public async Task ProcessAsync_FederatedProfileReauthWithToken_UsesFederatedLoginRequest(ClientType surface)
    {
        var validLogin = ValidServicePrincipalLogin();
        const string profileName = "ci";
        var clientId = validLogin.ClientId!;
        var tenantId = validLogin.TenantId!;
        var subscriptionId = validLogin.SubscriptionId!;
        var federatedToken = validLogin.FederatedToken!;

        var profiles = new Mock<IProfileService>();
        profiles.Setup(p => p.GetProfileAsync(profileName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Profile
                {
                    Name = profileName,
                    TenantId = tenantId,
                    SubscriptionId = subscriptionId,
                    CloudName = "AzureCloud",
                    AuthMethod = AuthMethod.Federated,
                    ClientId = clientId,
                });

        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.LoginAsync(
                It.Is<LoginRequest>(r =>
                    r.Credential is AuthCredential.FederatedIdentity &&
                    ((AuthCredential.FederatedIdentity)r.Credential).ClientId == clientId &&
                    ((AuthCredential.FederatedIdentity)r.Credential).Assertion == federatedToken &&
                    r.TenantId == tenantId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResult
            {
                AccessToken = "unit-test-access-token",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                TenantId = tenantId,
                Scopes = [],
            });

        var subscriptions = new Mock<ISubscriptionService>();
        subscriptions.Setup(s => s.GetSubscriptionByIdAsync(
                "unit-test-access-token", subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionInfo(subscriptionId, "Test", "Enabled"));

        InitializeProfileReauthDi(surface, profiles, auth, subscriptions);

        var processor = new LoginProcessor
        {
            Profile = profileName,
            FederatedToken = federatedToken,
        };
        ConfigureForDirectProcessAsync(processor, setWriteOutput: true);

        await processor.ProcessAsync();

        auth.VerifyAll();
        subscriptions.VerifyAll();
        profiles.Verify(p => p.SaveAndActivateProfileAsync(
            It.Is<Profile>(saved =>
                saved.Name == profileName &&
                saved.AuthMethod == AuthMethod.Federated &&
                saved.ClientId == clientId &&
                saved.TenantId == tenantId &&
                saved.SubscriptionId == subscriptionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public async Task ProcessAsync_NonFederatedProfileReauthWithFederatedToken_Throws(ClientType surface)
    {
        var validLogin = ValidServicePrincipalLogin();
        var profiles = new Mock<IProfileService>();
        profiles.Setup(p => p.GetProfileAsync("ci", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Profile
                {
                    Name = "ci",
                    TenantId = validLogin.TenantId!,
                    SubscriptionId = validLogin.SubscriptionId!,
                    CloudName = "AzureCloud",
                    AuthMethod = AuthMethod.Browser,
                });

        var auth = new Mock<IAuthService>();

        InitializeProfileReauthDi(surface, profiles, auth);

        var processor = new LoginProcessor
        {
            Profile = "ci",
            FederatedToken = validLogin.FederatedToken!,
        };
        ConfigureForDirectProcessAsync(processor);

        var ex = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);
        Assert.Contains(FlagFor(surface, nameof(LoginProcessor.FederatedToken)), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public async Task ProcessAsync_NonFederatedProfileReauthWithFederatedToken_DoesNotAttemptLoginOrSaveProfile(ClientType surface)
    {
        var validLogin = ValidServicePrincipalLogin();
        var profiles = new Mock<IProfileService>();
        profiles.Setup(p => p.GetProfileAsync("ci", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Profile
                {
                    Name = "ci",
                    TenantId = validLogin.TenantId!,
                    SubscriptionId = validLogin.SubscriptionId!,
                    CloudName = "AzureCloud",
                    AuthMethod = AuthMethod.Browser,
                });

        var auth = new Mock<IAuthService>();

        InitializeProfileReauthDi(surface, profiles, auth);

        var processor = new LoginProcessor
        {
            Profile = "ci",
            FederatedToken = validLogin.FederatedToken!,
        };
        ConfigureForDirectProcessAsync(processor);

        await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);

        auth.Verify(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        profiles.Verify(p => p.SaveAndActivateProfileAsync(It.IsAny<Profile>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
