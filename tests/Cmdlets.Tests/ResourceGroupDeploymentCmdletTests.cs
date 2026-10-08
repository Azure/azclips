// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.ArgumentParsing;
using Core.Runtime;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources;
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
}
