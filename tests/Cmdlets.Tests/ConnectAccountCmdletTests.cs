// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Security;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.DependencyInjection;
using Core.Runtime;
using Core.Runtime.Persistence;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts;
using Microsoft.Azure.Azclips.Extensions.Accounts.Models;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public class ConnectAccountCmdletTests
{
    private const string ValidSubscriptionId = "11111111-1111-1111-1111-111111111111";
    private const string ValidClientId = "aaaaaaaa-1111-1111-1111-111111111111";

    [Theory]
    [InlineData(null)]
    [InlineData("SubscriptionId")]
    [InlineData("Subscription")]
    public void CommandExecution_ProfileReauth_ShouldRejectExplicitButNotDefaultSubscription(string? subscriptionParameter)
    {
        using var fixture = new LoginCmdletFixture();
        fixture.Shell.AddParameter("Profile", "existing");
        if (subscriptionParameter is not null)
        {
            fixture.Shell.AddParameter(subscriptionParameter, ValidSubscriptionId);
        }

        Assert.Empty(fixture.Shell.Invoke());
        Assert.Empty(fixture.Shell.Streams.Error);
        var invocation = Assert.IsType<CommandInvocation>(fixture.Invocation);
        var metadata = new ExtensionIndex().GetCommandMetadata("Connect-AzAccount", ClientType.PowerShell);
        var service = new ArgumentParsingService(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));
        var processor = new LoginProcessor();
        service.BindArgumentsToProcessor(processor, invocation.Parameters, metadata,
            new Profile { SubscriptionId = ValidSubscriptionId });

        if (subscriptionParameter is null)
        {
            Assert.True(string.IsNullOrEmpty(processor.SubscriptionId));
            Assert.Equal("ProfileReauth", LoginProcessorTestHelpers.ResolveParameterSet(processor));
        }
        else
        {
            var ex = Assert.Throws<InvalidInputException>(() => LoginProcessorTestHelpers.ResolveParameterSet(processor));
            Assert.Contains("does not allow [-SubscriptionId]", ex.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Identity", false)]
    [InlineData("Identity", true)]
    [InlineData("MSI", false)]
    [InlineData("MSI", true)]
    [InlineData("ManagedIdentity", false)]
    [InlineData("ManagedIdentity", true)]
    [InlineData("ManagedService", false)]
    [InlineData("ManagedService", true)]
    public void CommandExecution_ManagedIdentity_ShouldRequireExplicitSubscription(string identityAlias, bool omitSubscription)
    {
        using var fixture = new LoginCmdletFixture();
        fixture.Shell.AddParameter(identityAlias);
        if (!omitSubscription)
        {
            fixture.Shell.AddParameter("Subscription", ValidSubscriptionId);
        }

        Assert.Empty(fixture.Shell.Invoke());
        Assert.Empty(fixture.Shell.Streams.Error);
        var invocation = Assert.IsType<CommandInvocation>(fixture.Invocation);
        var metadata = new ExtensionIndex().GetCommandMetadata("Connect-AzAccount", ClientType.PowerShell);
        var service = new ArgumentParsingService(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));
        var processor = new LoginProcessor();
        service.BindArgumentsToProcessor(processor, invocation.Parameters, metadata,
            new Profile { SubscriptionId = ValidSubscriptionId });

        Assert.True(processor.Identity);
        if (omitSubscription)
        {
            Assert.True(string.IsNullOrEmpty(processor.SubscriptionId));
            var ex = Assert.Throws<InvalidInputException>(() => LoginProcessorTestHelpers.ResolveParameterSet(processor));
            Assert.Contains("missing mandatory [-SubscriptionId]", ex.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(ValidSubscriptionId, processor.SubscriptionId);
            Assert.Equal("ManagedIdentity", LoginProcessorTestHelpers.ResolveParameterSet(processor));
        }
    }

    [Theory]
    [InlineData("Credential", typeof(PSCredential))]
    [InlineData("ClientSecret", typeof(SecureString))]
    public void Cmdlet_ShouldExposeSecureSecretParameters(string name, Type expectedType)
    {
        var property = typeof(ConnectAccountCmdlet).GetProperty(name);
        Assert.NotNull(property);
        Assert.Equal(expectedType, property.PropertyType);
        Assert.NotEmpty(property.GetCustomAttributes(typeof(ParameterAttribute), inherit: true));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void CommandExecution_ShouldNormalizeSecretInputs(bool useCredential, bool servicePrincipal)
    {
        using var fixture = new LoginCmdletFixture();
        using var secret = CreateSecret("test-secret");
        fixture.Shell.AddParameter("Tenant", "tenant-1")
            .AddParameter("Subscription", ValidSubscriptionId)
            .AddParameter("OutProfile", "ci");
        if (servicePrincipal) fixture.Shell.AddParameter("ServicePrincipal");
        if (useCredential)
        {
            fixture.Shell.AddParameter("Credential", new PSCredential(ValidClientId, secret));
        }
        else
        {
            fixture.Shell.AddParameter("ApplicationId", ValidClientId).AddParameter("ClientSecret", secret);
        }

        Assert.Empty(fixture.Shell.Invoke());
        Assert.Empty(fixture.Shell.Streams.Error);
        var invocation = Assert.IsType<CommandInvocation>(fixture.Invocation);
        Assert.Equal("Connect-AzAccount", invocation.CommandName);
        Assert.Equal(ValidClientId, invocation.Parameters["ClientId"]);
        Assert.Equal("test-secret", invocation.Parameters["ClientSecret"]);
        Assert.False(invocation.Parameters.ContainsKey("Credential"));
        Assert.Equal(servicePrincipal, invocation.Parameters.ContainsKey("ServicePrincipal"));

        var metadata = new ExtensionIndex().GetCommandMetadata("Connect-AzAccount", ClientType.PowerShell);
        var service = new ArgumentParsingService(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));
        var processor = new LoginProcessor();
        service.BindArgumentsToProcessor(processor, invocation.Parameters, metadata, new Profile());
        Assert.Equal(ValidClientId, processor.ClientId);
        Assert.Equal("test-secret", processor.ClientSecret);
        Assert.Equal("tenant-1", processor.TenantId);
        Assert.Equal(ValidSubscriptionId, processor.SubscriptionId);
        Assert.Equal("ci", processor.OutProfile);
        Assert.Equal("SPWithSecret", LoginProcessorTestHelpers.ResolveParameterSet(processor));
        fixture.Session.Verify(session => session.Run(), Times.Once);
    }

    [Theory]
    [InlineData("ClientId")]
    [InlineData("ClientSecret")]
    [InlineData("MissingServicePrincipal")]
    public void CommandExecution_ShouldRejectCredentialConflicts(string conflict)
    {
        using var fixture = new LoginCmdletFixture();
        using var secret = CreateSecret("test-secret");
        fixture.Shell.AddParameter("Credential", new PSCredential(ValidClientId, secret));
        if (conflict != "MissingServicePrincipal") fixture.Shell.AddParameter("ServicePrincipal");
        if (conflict == "ClientId") fixture.Shell.AddParameter("ClientId", ValidClientId);
        if (conflict == "ClientSecret") fixture.Shell.AddParameter("ClientSecret", secret);

        var error = Assert.ThrowsAny<RuntimeException>(() => fixture.Shell.Invoke());
        Assert.Contains(conflict == "MissingServicePrincipal" ? "ServicePrincipal" : conflict, error.Message);
        Assert.DoesNotContain("test-secret", error.Message);
        Assert.Null(fixture.Invocation);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CommandExecution_ShouldRejectEmptySecrets(bool useCredential)
    {
        using var fixture = new LoginCmdletFixture();
        using var secret = new SecureString();
        fixture.Shell.AddParameter("ServicePrincipal");
        if (useCredential)
            fixture.Shell.AddParameter("Credential", new PSCredential(ValidClientId, secret));
        else
            fixture.Shell.AddParameter("ClientId", ValidClientId).AddParameter("ClientSecret", secret);

        var error = Assert.ThrowsAny<RuntimeException>(() => fixture.Shell.Invoke());
        Assert.Contains("empty", error.Message);
        Assert.Null(fixture.Invocation);
    }

    [Fact]
    public void CommandExecution_ShouldRejectPlainTextClientSecret()
    {
        using var fixture = new LoginCmdletFixture();
        fixture.Shell.AddParameter("ClientSecret", "test-secret");

        Assert.ThrowsAny<RuntimeException>(() => fixture.Shell.Invoke());
        Assert.Null(fixture.Invocation);
    }

    private static SecureString CreateSecret(string value)
    {
        var secret = new SecureString();
        foreach (var character in value) secret.AppendChar(character);
        secret.MakeReadOnly();
        return secret;
    }

    private sealed class LoginCmdletFixture : IDisposable
    {
        private readonly Runspace runspace;
        public PowerShell Shell { get; }
        public CommandInvocation? Invocation { get; private set; }
        public Mock<ISession> Session { get; } = new();

        public LoginCmdletFixture()
        {
            AzclipsServiceProvider.Dispose();
            var engine = new Mock<IEngine>(MockBehavior.Strict);
            engine.Setup(engine => engine.OpenExecutionSession(
                    It.IsAny<CommandInvocation>(), It.IsAny<OutputStreams>(), It.IsAny<CancellationToken>()))
                .Callback<CommandInvocation, OutputStreams, CancellationToken>((invocation, _, _) => Invocation = invocation)
                .Returns(Session.Object);
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton(engine.Object);
                services.AddSingleton(Mock.Of<ITelemetryService>());
            });
            new Microsoft.Azure.Azclips.Cmdlets.Accounts.ModuleInitializer().OnImport();
            var state = InitialSessionState.CreateDefault2();
            state.Commands.Add(new SessionStateCmdletEntry("Connect-AzAccount", typeof(ConnectAccountCmdlet), null));
            runspace = RunspaceFactory.CreateRunspace(state);
            runspace.Open();
            Shell = PowerShell.Create();
            Shell.Runspace = runspace;
            Shell.AddCommand("Connect-AzAccount");
        }

        public void Dispose()
        {
            Shell.Dispose();
            runspace.Dispose();
            AzclipsServiceProvider.Dispose();
        }
    }

    // The managed-identity params must be reachable from Connect-AzAccount and bind to the
    // shared LoginProcessor exactly like the CLI surface does.
    [Fact]
    public void PSBinding_ShouldMapManagedIdentityArgsOntoLoginProcessor()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Connect-AzAccount", ClientType.PowerShell);
        var service = new ArgumentParsingService(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));
        var processor = new LoginProcessor();

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Identity"] = true,
            ["ClientId"] = "11111111-1111-1111-1111-111111111111",
            ["SubscriptionId"] = ValidSubscriptionId,
        };

        service.BindArgumentsToProcessor(processor, args, metadata, new Profile());

        Assert.True(processor.Identity);
        Assert.Equal("11111111-1111-1111-1111-111111111111", processor.ClientId);
        Assert.Equal(ValidSubscriptionId, processor.SubscriptionId);
    }

    [Theory]
    [InlineData("ObjectId", "22222222-2222-2222-2222-222222222222")]
    [InlineData("ResourceId", "/subscriptions/s/resourceGroups/r/providers/Microsoft.ManagedIdentity/userAssignedIdentities/u")]
    public void PSBinding_ShouldMapUserAssignedSelectors(string key, string value)
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Connect-AzAccount", ClientType.PowerShell);
        var service = new ArgumentParsingService(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService(), new FilePersistenceStore()));
        var processor = new LoginProcessor();

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Identity"] = true,
            [key] = value,
        };

        service.BindArgumentsToProcessor(processor, args, metadata, new Profile(){SubscriptionId = "sub-1"});

        var actual = (string?)typeof(LoginProcessor).GetProperty(key)!.GetValue(processor);
        Assert.Equal(value, actual);
        Assert.True(processor.Identity);
    }

    [Fact]
    public void Cmdlet_ShouldExposeManagedIdentityParameters()
    {
        var type = typeof(ConnectAccountCmdlet);

        foreach (var name in new[] { "Identity", "ClientId", "ObjectId", "ResourceId" })
        {
            var property = type.GetProperty(name);
            Assert.NotNull(property);
            Assert.NotEmpty(property!.GetCustomAttributes(typeof(ParameterAttribute), inherit: true));
        }
    }

    [Fact]
    public void Cmdlet_OutputType_ShouldUseProfileResultProjection()
    {
        var outputType = (OutputTypeAttribute)typeof(ConnectAccountCmdlet)
            .GetCustomAttributes(typeof(OutputTypeAttribute), inherit: true)
            .Single();

        Assert.Contains(
            outputType.Type,
            typeName => string.Equals(
                typeName.Name,
                "Microsoft.Azure.Azclips.Extensions.Accounts.Models.ProfileResult",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ProfileResult_ShouldOmitRuntimeMetadata()
    {
        var profile = new Profile
        {
            Name = "prod",
            TenantId = "tenant-1",
            SubscriptionId = ValidSubscriptionId,
            CloudName = "AzureCloud",
            AuthMethod = AuthMethod.Browser,
            DefaultLocation = "westus",
            DefaultResourceGroup = "rg",
            UserPrincipalName = "user@microsoft.com",
            ClientId = "client-1",
            ManagedIdentityId = "MSIObject@22222222-2222-2222-2222-222222222222",
            Metadata = new ProfileRuntimeMetadata
            {
                EntraObjectId = "33333333-3333-3333-3333-333333333333",
            },
        };

        var output = ProfileResult.From(profile);

        Assert.Equal(profile.Name, output.Name);
        Assert.Equal(profile.TenantId, output.TenantId);
        Assert.Equal(profile.SubscriptionId, output.SubscriptionId);
        Assert.Equal(profile.CloudName, output.CloudName);
        Assert.Equal(profile.AuthMethod, output.AuthMethod);
        Assert.False(output.IsActive);
        Assert.Equal(profile.DefaultLocation, output.DefaultLocation);
        Assert.Equal(profile.DefaultResourceGroup, output.DefaultResourceGroup);
        Assert.Equal(profile.UserPrincipalName, output.UserPrincipalName);
        Assert.Equal(profile.ClientId, output.ClientId);
        Assert.Equal(profile.ManagedIdentityId, output.ManagedIdentityId);
        Assert.Null(typeof(ProfileResult).GetProperty("Metadata"));
    }

    [Fact]
    public void Cmdlet_IdentityParameter_ShouldHaveMsiAliases()
    {
        var alias = (AliasAttribute?)typeof(ConnectAccountCmdlet)
            .GetProperty("Identity")!
            .GetCustomAttributes(typeof(AliasAttribute), inherit: true)
            .SingleOrDefault();

        Assert.NotNull(alias);
        Assert.Contains("MSI", alias!.AliasNames);
        Assert.Contains("ManagedIdentity", alias.AliasNames);
        // Az PowerShell's own alias for -Identity; existing scripts depend on it.
        Assert.Contains("ManagedService", alias.AliasNames);
    }

    // The PS binder resolves -Subscription/-Tenant/-DeviceCode etc. via [Alias] on the cmdlet
    // property, so a missing alias silently breaks Az-parity invocations at bind time.
    [Theory]
    [InlineData("SubscriptionId", new[] { "Subscription" })]
    [InlineData("TenantId", new[] { "Tenant" })]
    [InlineData("UseDeviceCode", new[] { "DeviceCode", "DeviceAuth", "Device", "UseDeviceAuthentication" })]
    public void Cmdlet_Parameter_ShouldHaveClassicAliases(string property, string[] expectedAliases)
    {
        var alias = (AliasAttribute?)typeof(ConnectAccountCmdlet)
            .GetProperty(property)!
            .GetCustomAttributes(typeof(AliasAttribute), inherit: true)
            .SingleOrDefault();

        Assert.NotNull(alias);
        foreach (var expected in expectedAliases)
        {
            Assert.Contains(expected, alias!.AliasNames);
        }
    }

    [Fact]
    public void PSMetadata_ShouldResolveLegacyManagedIdentityAliases()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Connect-AzAccount", ClientType.PowerShell);

        Assert.Equal("Identity", metadata.ParameterAliases["ManagedService"]);
        Assert.Equal("Identity", metadata.ParameterAliases["MSI"]);
        Assert.Equal("Identity", metadata.ParameterAliases["ManagedIdentity"]);
        Assert.Contains("ManagedService", metadata.Parameters["Identity"].Aliases);
    }

    [Fact]
    public void PSMetadata_ShouldResolveClassicAliases()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Connect-AzAccount", ClientType.PowerShell);

        Assert.Equal("SubscriptionId", metadata.ParameterAliases["Subscription"]);
        Assert.Equal("TenantId", metadata.ParameterAliases["Tenant"]);
        Assert.Equal("UseDeviceCode", metadata.ParameterAliases["DeviceCode"]);
        Assert.Equal("UseDeviceCode", metadata.ParameterAliases["DeviceAuth"]);
        Assert.Equal("UseDeviceCode", metadata.ParameterAliases["Device"]);
        Assert.Equal("UseDeviceCode", metadata.ParameterAliases["UseDeviceAuthentication"]);

        Assert.Contains("Subscription", metadata.Parameters["SubscriptionId"].Aliases);
        Assert.Contains("Tenant", metadata.Parameters["TenantId"].Aliases);
        Assert.Contains("UseDeviceAuthentication", metadata.Parameters["UseDeviceCode"].Aliases);
    }

    [Fact]
    public void Cmdlet_SubscriptionId_ShouldNotBeMandatory()
    {
        // MI login discovers a default subscription, so -SubscriptionId must be optional.
        var parameter = (ParameterAttribute)typeof(ConnectAccountCmdlet)
            .GetProperty("SubscriptionId")!
            .GetCustomAttributes(typeof(ParameterAttribute), inherit: true)
            .Single();

        Assert.False(parameter.Mandatory);
    }
}
