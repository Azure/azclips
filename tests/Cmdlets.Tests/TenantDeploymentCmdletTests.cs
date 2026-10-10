// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Reflection;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cmdlets.Tests;

public class TenantDeploymentCmdletTests
{
    [Theory]
    [InlineData("/providers/Microsoft.Resources/deployments/deployment", "deployment")]
    [InlineData("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Resources/deployments/deployment", "deployment")]
    [InlineData("/providers/Microsoft.Resources/deployments/parent/child/nested", "parent/nested")]
    [InlineData("/providers/Microsoft.Resources/deployments", "")]
    [InlineData("deployment", "")]
    public void GetDeploymentName_MatchesLegacyResourceIdUtility(string id, string expected)
    {
        MethodInfo method = typeof(TenantDeploymentCmdletBase).GetMethod("GetDeploymentName", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(expected, method.Invoke(null, [id]));
    }

    [Theory]
    [InlineData(typeof(NewTenantDeploymentCmdlet), typeof(PSDeployment))]
    [InlineData(typeof(TestTenantDeploymentCmdlet), typeof(PSResourceManagerError))]
    [InlineData(typeof(StopTenantDeploymentCmdlet), typeof(bool))]
    [InlineData(typeof(RemoveTenantDeploymentCmdlet), typeof(bool))]
    public void Cmdlets_DeclareLegacyOutputTypes(Type cmdlet, Type expected)
    {
        Assert.Equal(expected, Assert.Single(Assert.Single(cmdlet.GetCustomAttributes<OutputTypeAttribute>()).Type).Type);
    }

    [Fact]
    public void GetCmdlet_DeclaresPSDeploymentOutputByName()
    {
        Assert.Equal(
            typeof(PSDeployment).FullName,
            Assert.Single(Assert.Single(typeof(GetTenantDeploymentCmdlet).GetCustomAttributes<OutputTypeAttribute>()).Type).Name);
    }

    [Theory]
    [InlineData(typeof(NewTenantDeploymentCmdlet))]
    [InlineData(typeof(TestTenantDeploymentCmdlet))]
    public void TemplateCmdlets_UseLegacyParameterSets(Type cmdlet)
    {
        Assert.Equal("ByTemplateFileWithNoParameters", cmdlet.GetCustomAttribute<CmdletAttribute>()!.DefaultParameterSetName);
        Assert.Equal(
            [
                "ByParameterFileWithNoTemplate",
                "ByTemplateFileAndParameterFile",
                "ByTemplateFileAndParameterObject",
                "ByTemplateFileAndParameterUri",
                "ByTemplateFileWithNoParameters",
                "ByTemplateObjectAndParameterFile",
                "ByTemplateObjectAndParameterObject",
                "ByTemplateObjectAndParameterUri",
                "ByTemplateObjectWithNoParameters",
                "ByTemplateSpecResourceId",
                "ByTemplateSpecResourceIdAndParams",
                "ByTemplateSpecResourceIdAndParamsObject",
                "ByTemplateSpecResourceIdAndParamsUri",
                "ByTemplateUriAndParameterFile",
                "ByTemplateUriAndParameterObject",
                "ByTemplateUriAndParameterUri",
                "ByTemplateUriWithNoParameters",
            ],
            ParameterSets(cmdlet));
        Assert.Contains("DeploymentName", Aliases(cmdlet, "Name"));
    }

    [Theory]
    [InlineData(typeof(GetTenantDeploymentCmdlet), "GetByDeploymentName", new[] { "GetByDeploymentId", "GetByDeploymentName" })]
    [InlineData(typeof(StopTenantDeploymentCmdlet), "StopByDeploymentName", new[] { "StopByDeploymentId", "StopByDeploymentName", "StopByInputObject" })]
    [InlineData(typeof(RemoveTenantDeploymentCmdlet), "RemoveByDeploymentName", new[] { "RemoveByDeploymentId", "RemoveByDeploymentName", "RemoveByInputObject" })]
    public void NameCmdlets_UseLegacyParameterSetsAndAliases(Type cmdlet, string defaultSet, string[] sets)
    {
        Assert.Equal(defaultSet, cmdlet.GetCustomAttribute<CmdletAttribute>()!.DefaultParameterSetName);
        Assert.Equal(sets, ParameterSets(cmdlet));
        Assert.Equal(["DeploymentName"], Aliases(cmdlet, "Name"));
        Assert.Equal(["DeploymentId", "ResourceId"], Aliases(cmdlet, "Id"));
    }

    [Theory]
    [InlineData(typeof(NewTenantDeploymentCmdlet), true)]
    [InlineData(typeof(RemoveTenantDeploymentCmdlet), true)]
    [InlineData(typeof(StopTenantDeploymentCmdlet), false)]
    [InlineData(typeof(TestTenantDeploymentCmdlet), false)]
    [InlineData(typeof(GetTenantDeploymentCmdlet), false)]
    public void AsJob_IsExposedOnlyWhereLegacyHasIt(Type cmdlet, bool expected)
    {
        Assert.Equal(expected, cmdlet.GetProperty("AsJob") is not null);
    }

    [Fact]
    public void NewCmdlet_ExposesLegacyWhatIfAndDeploymentParameters()
    {
        Type cmdlet = typeof(NewTenantDeploymentCmdlet);

        Assert.Equal(["FullResourcePayloads", "ResourceIdOnly"], cmdlet.GetProperty("WhatIfResultFormat")!.GetCustomAttribute<ValidateSetAttribute>()!.ValidValues);
        Assert.Equal(
            ["RequestContent", "ResponseContent", "All", "None"],
            cmdlet.GetProperty("DeploymentDebugLogLevel")!.GetCustomAttribute<ArgumentCompletionsAttribute>()!.CompleteArgument("New-AzTenantDeployment", "DeploymentDebugLogLevel", "", null!, null!).Select(c => c.CompletionText));
        Assert.NotNull(cmdlet.GetProperty("Tag"));
        Assert.NotNull(cmdlet.GetProperty("WhatIfExcludeChangeType"));
        Assert.NotNull(cmdlet.GetProperty("ProceedIfNoChange"));
        Assert.True(cmdlet.GetCustomAttribute<CmdletAttribute>()!.SupportsShouldProcess);
        Assert.NotNull(typeof(TestTenantDeploymentCmdlet).GetProperty("SkipTemplateParameterPrompt"));
    }

    private static string[] ParameterSets(Type cmdlet) =>
        cmdlet.GetProperties()
            .SelectMany(p => p.GetCustomAttributes<ParameterAttribute>())
            .Select(p => p.ParameterSetName)
            .Where(name => name != ParameterAttribute.AllParameterSets)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> Aliases(Type cmdlet, string property) =>
        cmdlet.GetProperty(property)!.GetCustomAttribute<AliasAttribute>()!.AliasNames;
}
