// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Management.Automation;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Runtime;
using Microsoft.Azure.Azclips.Cmdlets.Login;
using Microsoft.Azure.Azclips.Extensions.Login;
using Microsoft.Azure.Azclips.Extensions.Login.Processors;

namespace Cmdlets.Tests;

public class ConnectAccountCmdletTests
{
    // The managed-identity params must be reachable from Connect-AzAccount and bind to the
    // shared LoginProcessor exactly like the CLI surface does.
    [Fact]
    public void PSBinding_ShouldMapManagedIdentityArgsOntoLoginProcessor()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Connect-AzAccount", ClientType.PowerShell);
        var service = new ArgumentParsingService(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService()));
        var processor = new LoginProcessor();

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Identity"] = true,
            ["ClientId"] = "11111111-1111-1111-1111-111111111111",
            ["SubscriptionId"] = "sub-1",
        };

        service.BindArgumentsToProcessor(processor, args, metadata.Parameters, metadata.ParameterAliases, new Profile());

        Assert.True(processor.Identity);
        Assert.Equal("11111111-1111-1111-1111-111111111111", processor.ClientId);
        Assert.Equal("sub-1", processor.SubscriptionId);
    }

    [Theory]
    [InlineData("ObjectId", "22222222-2222-2222-2222-222222222222")]
    [InlineData("ResourceId", "/subscriptions/s/resourceGroups/r/providers/Microsoft.ManagedIdentity/userAssignedIdentities/u")]
    public void PSBinding_ShouldMapUserAssignedSelectors(string key, string value)
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Connect-AzAccount", ClientType.PowerShell);
        var service = new ArgumentParsingService(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService()));
        var processor = new LoginProcessor();

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Identity"] = true,
            [key] = value,
        };

        service.BindArgumentsToProcessor(processor, args, metadata.Parameters, metadata.ParameterAliases, new Profile(){SubscriptionId = "sub-1"});

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

    [Fact]
    public void Cmdlet_ClientId_ShouldAliasAccountId()
    {
        // Connect-AzAccount -Identity -AccountId <clientId> is the Az PowerShell spelling.
        var alias = (AliasAttribute?)typeof(ConnectAccountCmdlet)
            .GetProperty("ClientId")!
            .GetCustomAttributes(typeof(AliasAttribute), inherit: true)
            .SingleOrDefault();

        Assert.NotNull(alias);
        Assert.Contains("AccountId", alias!.AliasNames);
    }

    [Fact]
    public void PSMetadata_ShouldResolveLegacyManagedIdentityAliases()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Connect-AzAccount", ClientType.PowerShell);

        Assert.Equal("Identity", metadata.ParameterAliases["ManagedService"]);
        Assert.Equal("Identity", metadata.ParameterAliases["MSI"]);
        Assert.Equal("Identity", metadata.ParameterAliases["ManagedIdentity"]);
        Assert.Equal("ClientId", metadata.ParameterAliases["AccountId"]);
        Assert.Contains("ManagedService", metadata.Parameters["Identity"].Aliases);
        Assert.Contains("AccountId", metadata.Parameters["ClientId"].Aliases);
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
