// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Runtime;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Azure.Azclips.Cmdlets.ManagedServiceIdentity;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Processors;

namespace Cmdlets.Tests;

/// <summary>
/// PowerShell-surface coverage for managed identity cmdlets: parameter-set selection and
/// metadata-to-processor binding for create/list/get/update/delete command routes.
/// </summary>
public class ManagedServiceIdentityCmdletTests
{
    private static ArgumentParsingService PSBinder() =>
        new(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService()));

    [Theory]
    [InlineData("Target", "List")]
    [InlineData("Target -ResourceGroupName rg1", "List")]
    [InlineData("Target -ResourceGroupName rg1 -Name id1", "Get")]
    public void GetCmdlet_SelectsExpectedParameterSet(string invocation, string expectedSet)
    {
        var (parameterSet, _) = BindWithSet(typeof(GetAzUserAssignedIdentityCmdlet), invocation);
        Assert.Equal(expectedSet, parameterSet);
    }

    [Fact]
    public void GetCmdlet_DefaultParameterSet_IsList()
    {
        var cmdlet = typeof(GetAzUserAssignedIdentityCmdlet).GetCustomAttributes(typeof(CmdletAttribute), inherit: true)
            .Cast<CmdletAttribute>()
            .Single();

        Assert.Equal("List", cmdlet.DefaultParameterSetName);
    }

    [Fact]
    public void PSBinding_GetRoute_MapsToShowProcessorProperties()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzUserAssignedIdentity_Get", ClientType.PowerShell);
        var processor = new ShowIdentityProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubscriptionId"] = "sub-1",
                ["ResourceGroupName"] = "rg1",
                ["Name"] = "id1",
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal(typeof(ShowIdentityProcessor), metadata.ProcessorType);
        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("rg1", processor.ResourceGroup);
        Assert.Equal("id1", processor.ResourceName);
    }

    [Fact]
    public void PSBinding_ListRoute_MapsToListProcessorProperties()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzUserAssignedIdentity_List", ClientType.PowerShell);
        var processor = new ListIdentityProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubscriptionId"] = "sub-1",
                ["ResourceGroupName"] = "rg1",
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal(typeof(ListIdentityProcessor), metadata.ProcessorType);
        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("rg1", processor.ResourceGroup);
    }

    [Fact]
    public void PSBinding_NewRoute_MapsTagAndRestrictionToCreateProcessor()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("New-AzUserAssignedIdentity", ClientType.PowerShell);
        var processor = new CreateIdentityProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubscriptionId"] = "sub-1",
                ["ResourceGroupName"] = "rg1",
                ["Name"] = "id1",
                ["Location"] = "eastus",
                ["Tags"] = new Dictionary<string, string> { ["env"] = "test" },
                ["ResourceRestriction"] = new[] { "Microsoft.Compute" },
                ["IsolationScope"] = "Regional",
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal(typeof(CreateIdentityProcessor), metadata.ProcessorType);
        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("rg1", processor.ResourceGroup);
        Assert.Equal("id1", processor.ResourceName);
        Assert.Equal("eastus", processor.Location);
        Assert.Equal("test", processor.Tags!["env"]);
        Assert.Equal(["Microsoft.Compute"], processor.ResourceRestrictionProviders);
        Assert.Equal("Regional", processor.IsolationScope);
    }

    [Fact]
    public void PSBinding_UpdateRoute_MapsTagAndRestrictionToUpdateProcessor()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Update-AzUserAssignedIdentity", ClientType.PowerShell);
        var processor = new UpdateIdentityProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubscriptionId"] = "sub-1",
                ["ResourceGroupName"] = "rg1",
                ["Name"] = "id1",
                ["Location"] = "eastus2",
                ["Tags"] = new Dictionary<string, string> { ["env"] = "prod" },
                ["ResourceRestriction"] = new[] { "Microsoft.Storage/accounts" },
                ["IsolationScope"] = "None",
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal(typeof(UpdateIdentityProcessor), metadata.ProcessorType);
        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("rg1", processor.ResourceGroup);
        Assert.Equal("id1", processor.ResourceName);
        Assert.Equal("eastus2", processor.Location);
        Assert.Equal("prod", processor.Tags!["env"]);
        Assert.Equal(["Microsoft.Storage/accounts"], processor.ResourceRestrictionProviders);
        Assert.Equal("None", processor.IsolationScope);
    }

    [Fact]
    public void PSBinding_RemoveRoute_MapsToDeleteProcessorProperties()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("Remove-AzUserAssignedIdentity", ClientType.PowerShell);
        var processor = new DeleteIdentityProcessor();

        PSBinder().BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["SubscriptionId"] = "sub-1",
                ["ResourceGroupName"] = "rg1",
                ["Name"] = "id1",
            },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile());

        Assert.Equal(typeof(DeleteIdentityProcessor), metadata.ProcessorType);
        Assert.Equal("sub-1", processor.SubscriptionId);
        Assert.Equal("rg1", processor.ResourceGroup);
        Assert.Equal("id1", processor.ResourceName);
    }

    private static (string ParameterSet, Hashtable Bound) BindWithSet(Type cmdlet, string invocation)
    {
        string paramBlock = ProxyCommand.GetCmdletBindingAttribute(new System.Management.Automation.CommandMetadata(cmdlet))
            + "param(" + ProxyCommand.GetParamBlock(new System.Management.Automation.CommandMetadata(cmdlet)) + ")";
        var iss = System.Management.Automation.Runspaces.InitialSessionState.Create();
        iss.LanguageMode = PSLanguageMode.FullLanguage;
        using var ps = PowerShell.Create(iss);
        ps.AddScript($"function Target {{ {paramBlock} process {{ $PSCmdlet.ParameterSetName; $PSBoundParameters }} }}; {invocation}");
        var result = ps.Invoke();
        Assert.Empty(ps.Streams.Error);
        Assert.Equal(2, result.Count);

        var bound = new Hashtable(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in (IDictionary)result[1].BaseObject)
        {
            bound[entry.Key] = entry.Value is PSObject pso ? pso.BaseObject : entry.Value;
        }

        return ((string)result[0].BaseObject, bound);
    }
}
