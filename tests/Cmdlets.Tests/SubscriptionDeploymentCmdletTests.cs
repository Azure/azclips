// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using static Cmdlets.Tests.ResourceGroupDeploymentCmdletTests;

namespace Cmdlets.Tests;

/// <summary>Legacy Az.Resources names and binding for the subscription-scope <c>*-AzDeployment</c> cmdlets.</summary>
public class SubscriptionDeploymentCmdletTests
{
    [Theory]
    [InlineData(typeof(NewDeploymentCmdlet), "New-AzSubscriptionDeployment")]
    [InlineData(typeof(TestDeploymentCmdlet), "Test-AzSubscriptionDeployment")]
    [InlineData(typeof(GetDeploymentCmdlet), "Get-AzSubscriptionDeployment")]
    [InlineData(typeof(StopDeploymentCmdlet), "Stop-AzSubscriptionDeployment")]
    [InlineData(typeof(RemoveDeploymentCmdlet), "Remove-AzSubscriptionDeployment")]
    public void Cmdlet_HasLegacyCommandAlias(Type cmdlet, string legacyName)
    {
        var alias = (AliasAttribute)Assert.Single(cmdlet.GetCustomAttributes(typeof(AliasAttribute), inherit: false));

        Assert.Contains(legacyName, alias.AliasNames);
    }

    [Theory]
    [InlineData(typeof(NewDeploymentCmdlet), "Name", "DeploymentName")]
    [InlineData(typeof(NewDeploymentCmdlet), "ParametersFile", "TemplateParameterFile")]
    [InlineData(typeof(NewDeploymentCmdlet), "ParametersUri", "TemplateParameterUri")]
    [InlineData(typeof(TestDeploymentCmdlet), "Name", "DeploymentName")]
    [InlineData(typeof(TestDeploymentCmdlet), "ParametersFile", "TemplateParameterFile")]
    [InlineData(typeof(TestDeploymentCmdlet), "ParametersUri", "TemplateParameterUri")]
    [InlineData(typeof(GetDeploymentCmdlet), "Name", "DeploymentName")]
    [InlineData(typeof(GetDeploymentCmdlet), "Id", "DeploymentId")]
    [InlineData(typeof(GetDeploymentCmdlet), "Id", "ResourceId")]
    [InlineData(typeof(StopDeploymentCmdlet), "Name", "DeploymentName")]
    [InlineData(typeof(RemoveDeploymentCmdlet), "Name", "DeploymentName")]
    public void Cmdlet_Parameter_HasLegacyAlias(Type cmdlet, string property, string legacyName)
    {
        var alias = (AliasAttribute)Assert.Single(cmdlet.GetProperty(property)!.GetCustomAttributes(typeof(AliasAttribute), inherit: true));

        Assert.Contains(legacyName, alias.AliasNames);
    }

    [Theory]
    [InlineData(typeof(GetDeploymentCmdlet))]
    [InlineData(typeof(StopDeploymentCmdlet))]
    [InlineData(typeof(RemoveDeploymentCmdlet))]
    public void NameBinds_PositionallyAndFromPipeline(Type cmdlet)
    {
        Assert.Equal("dep", Bind(cmdlet, "Target dep")["Name"]);
        Assert.Equal("dep", Bind(cmdlet, "Target", new DeploymentExtended { Name = "dep" })["Name"]);
    }
}
