// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Runtime;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cmdlets.Tests;

/// <summary>
/// Legacy Az.Resources parameter names on the resource-group deployment cmdlets. PowerShell resolves
/// these through <see cref="AliasAttribute"/> on the cmdlet property, so existing scripts break at
/// bind time if an alias is missing.
/// </summary>
public class ResourceGroupDeploymentCmdletTests
{
    [Theory]
    [InlineData(typeof(TestResourceGroupDeploymentCmdlet), "ParametersFile", "TemplateParameterFile")]
    [InlineData(typeof(TestResourceGroupDeploymentCmdlet), "ParametersUri", "TemplateParameterUri")]
    [InlineData(typeof(TestResourceGroupDeploymentCmdlet), "RollbackOnError", "RollbackToLastDeployment")]
    [InlineData(typeof(TestResourceGroupDeploymentCmdlet), "RollbackToDeploymentName", "RollBackDeploymentName")]
    [InlineData(typeof(GetResourceGroupDeploymentCmdlet), "Name", "DeploymentName")]
    [InlineData(typeof(GetResourceGroupDeploymentCmdlet), "Id", "DeploymentId")]
    [InlineData(typeof(GetResourceGroupDeploymentCmdlet), "Id", "ResourceId")]
    public void Cmdlet_Parameter_HasLegacyAlias(Type cmdlet, string property, string legacyName)
    {
        var alias = (AliasAttribute?)cmdlet
            .GetProperty(property)!
            .GetCustomAttributes(typeof(AliasAttribute), inherit: true)
            .SingleOrDefault();

        Assert.NotNull(alias);
        Assert.Contains(legacyName, alias!.AliasNames);
    }

    [Fact]
    public void GetCmdlet_Id_IsMandatoryInGetByIdSetOnly()
    {
        var sets = typeof(GetResourceGroupDeploymentCmdlet)
            .GetProperty("Id")!
            .GetCustomAttributes(typeof(ParameterAttribute), inherit: true)
            .Cast<ParameterAttribute>()
            .ToList();

        var set = Assert.Single(sets);
        Assert.Equal("GetById", set.ParameterSetName);
        Assert.True(set.Mandatory);
    }

    [Fact]
    public void PSBinding_GetById_BindsIdOntoOrchestrator()
    {
        const string id = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Resources/deployments/dep1";
        var metadata = new ExtensionIndex().GetCommandMetadata("Get-AzResourceGroupDeployment_GetById", ClientType.PowerShell);
        var service = new ArgumentParsingService(new PSArgumentParsingProvider(), new ProfileService(new ScopedLoggingService()));
        var processor = (GetResourceGroupDeploymentProcessor_Orchestrator)metadata.GetInstance();

        service.BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["Id"] = id },
            metadata.Parameters,
            metadata.ParameterAliases,
            new Profile { SubscriptionId = "sub" });

        Assert.Equal(id, processor.Id);
    }

    // Runs the real PowerShell binder against each cmdlet's exact parameter metadata, via a stand-in
    // function that returns $PSBoundParameters, so binding is verified without starting the engine.
    private static Hashtable Bind(Type cmdlet, string invocation, object? pipelineInput = null)
        => BindWithSet(cmdlet, invocation, pipelineInput).Bound;

    private static (string ParameterSet, Hashtable Bound) BindWithSet(Type cmdlet, string invocation, object? pipelineInput = null)
    {
        string paramBlock = ProxyCommand.GetCmdletBindingAttribute(new System.Management.Automation.CommandMetadata(cmdlet))
            + "param(" + ProxyCommand.GetParamBlock(new System.Management.Automation.CommandMetadata(cmdlet)) + ")";
        var iss = System.Management.Automation.Runspaces.InitialSessionState.Create();
        iss.LanguageMode = PSLanguageMode.FullLanguage;
        using var ps = PowerShell.Create(iss);
        ps.AddScript($"function Target {{ {paramBlock} process {{ $PSCmdlet.ParameterSetName; $PSBoundParameters }} }}; " + (pipelineInput is null ? invocation : $"$input | {invocation}"));
        var result = pipelineInput is null ? ps.Invoke() : ps.Invoke(new[] { pipelineInput });
        Assert.Empty(ps.Streams.Error);
        Assert.Equal(2, result.Count);
        var bound = new Hashtable(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry e in (IDictionary)result[1].BaseObject)
        {
            bound[e.Key] = e.Value is PSObject pso ? pso.BaseObject : e.Value;
        }
        return ((string)result[0].BaseObject, bound);
    }

    [Theory]
    [InlineData("Target rg", "List")]
    [InlineData("Target rg dep", "Get")]
    [InlineData("Target -ResourceGroupName rg -Name dep", "Get")]
    public void GetCmdlet_SelectsParameterSet(string invocation, string expectedSet)
    {
        Assert.Equal(expectedSet, BindWithSet(typeof(GetResourceGroupDeploymentCmdlet), invocation).ParameterSet);
    }

    [Fact]
    public void GetCmdlet_PipedDeployment_SelectsGet()
    {
        var (set, _) = BindWithSet(typeof(GetResourceGroupDeploymentCmdlet), "Target", new DeploymentExtended
        {
            Id = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Resources/deployments/dep",
            Name = "dep",
        });

        Assert.Equal("Get", set);
    }

    [Theory]
    [InlineData(typeof(GetResourceGroupDeploymentCmdlet))]
    [InlineData(typeof(StopResourceGroupDeploymentCmdlet))]
    [InlineData(typeof(RemoveResourceGroupDeploymentCmdlet))]
    [InlineData(typeof(SaveResourceGroupDeploymentTemplateCmdlet))]
    public void Pipeline_DeploymentObject_BindsResourceGroupAndName(Type cmdlet)
    {
        var bound = Bind(cmdlet, "Target", new DeploymentExtended
        {
            Id = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Resources/deployments/dep",
            Name = "dep",
        });

        Assert.Equal("rg", bound["ResourceGroupName"]);
        Assert.Equal("dep", bound["Name"]);
    }

    [Fact]
    public void Pipeline_DeploymentObject_BindsTestResourceGroupAndMode()
    {
        var bound = Bind(typeof(TestResourceGroupDeploymentCmdlet), "Target", new DeploymentExtended
        {
            Id = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Resources/deployments/dep",
            Properties = new DeploymentPropertiesExtended { Mode = DeploymentMode.Incremental },
        });

        Assert.Equal("rg", bound["ResourceGroupName"]);
        Assert.Equal("Incremental", bound["Mode"]);
    }

    [Theory]
    [InlineData(typeof(GetResourceGroupDeploymentCmdlet))]
    [InlineData(typeof(StopResourceGroupDeploymentCmdlet))]
    [InlineData(typeof(RemoveResourceGroupDeploymentCmdlet))]
    public void Positional_BindsResourceGroupThenName(Type cmdlet)
    {
        var bound = Bind(cmdlet, "Target rg dep");

        Assert.Equal("rg", bound["ResourceGroupName"]);
        Assert.Equal("dep", bound["Name"]);
    }
}
