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
    public void PSBinding_ShouldMapDescriptionOntoLoginProcessor()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Connect-AzAccount", ClientType.PowerShell);
        var service = new ArgumentParsingService(new PSArgumentParsingProvider());
        var processor = new LoginProcessor();

        var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["OutProfile"] = "prod",
            ["Description"] = "read-only prod",
            ["SubscriptionId"] = "sub-1",
        };

        service.BindArgumentsToProcessor(processor, args, metadata.Parameters, metadata.ParameterAliases);

        Assert.Equal("read-only prod", processor.Description);
        Assert.Equal("prod", processor.OutProfile);
    }

    [Fact]
    public void Cmdlet_ShouldExposeDescriptionParameter()
    {
        var property = typeof(ConnectAccountCmdlet).GetProperty("Description");
        Assert.NotNull(property);
        Assert.NotEmpty(property!.GetCustomAttributes(typeof(ParameterAttribute), inherit: true));
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
