// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Reflection;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cmdlets.Tests;

public class ManagementGroupDeploymentCmdletTests
{
    [Theory]
    [InlineData("/providers/Microsoft.Resources/deployments/deployment", "deployment")]
    [InlineData("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Resources/deployments/deployment", "deployment")]
    [InlineData("/providers/Microsoft.Resources/deployments/parent/child/nested", "parent/nested")]
    [InlineData("/providers/Microsoft.Resources/deployments", "")]
    [InlineData("deployment", "")]
    public void GetDeploymentName_MatchesLegacyResourceIdUtility(string id, string expected)
    {
        MethodInfo method = typeof(ManagementGroupDeploymentCmdletBase).GetMethod("GetDeploymentName", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(expected, method.Invoke(null, [id]));
    }

    [Theory]
    [InlineData("/providers/Microsoft.Management/managementGroups/mg1/providers/Microsoft.Resources/deployments/deployment", "mg1")]
    [InlineData("/PROVIDERS/MICROSOFT.MANAGEMENT/MANAGEMENTGROUPS/mg1", "mg1")]
    [InlineData("/providers/Microsoft.Management/managementGroups/", null)]
    [InlineData("/providers/Microsoft.Resources/deployments/deployment", null)]
    [InlineData("/subscriptions/sub/providers/Microsoft.Management/managementGroups/mg1", null)]
    public void GetManagementGroupId_MatchesLegacyResourceIdUtility(string id, string? expected)
    {
        MethodInfo method = typeof(ManagementGroupDeploymentCmdletBase).GetMethod("GetManagementGroupId", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(expected, method.Invoke(null, [id]));
    }

    [Theory]
    [InlineData(typeof(NewManagementGroupDeploymentCmdlet), ParameterAttribute.AllParameterSets, int.MinValue)]
    [InlineData(typeof(TestManagementGroupDeploymentCmdlet), ParameterAttribute.AllParameterSets, int.MinValue)]
    [InlineData(typeof(GetManagementGroupDeploymentCmdlet), "GetByDeploymentName", 0)]
    [InlineData(typeof(StopManagementGroupDeploymentCmdlet), "StopByDeploymentName", 0)]
    [InlineData(typeof(RemoveManagementGroupDeploymentCmdlet), "RemoveByDeploymentName", 0)]
    public void ManagementGroupId_IsMandatoryLikeLegacy(Type cmdlet, string set, int position)
    {
        ParameterAttribute parameter = Assert.Single(cmdlet.GetProperty("ManagementGroupId")!.GetCustomAttributes<ParameterAttribute>());

        Assert.Equal((true, set, position), (parameter.Mandatory, parameter.ParameterSetName, parameter.Position));
    }

    [Theory]
    [InlineData(typeof(NewManagementGroupDeploymentCmdlet), typeof(PSDeployment))]
    [InlineData(typeof(TestManagementGroupDeploymentCmdlet), typeof(PSResourceManagerError))]
    [InlineData(typeof(StopManagementGroupDeploymentCmdlet), typeof(bool))]
    [InlineData(typeof(RemoveManagementGroupDeploymentCmdlet), typeof(bool))]
    public void Cmdlets_DeclareLegacyOutputTypes(Type cmdlet, Type expected)
    {
        Assert.Equal(expected, Assert.Single(Assert.Single(cmdlet.GetCustomAttributes<OutputTypeAttribute>()).Type).Type);
    }

    [Fact]
    public void GetCmdlet_DeclaresPSDeploymentOutputByName()
    {
        Assert.Equal(
            typeof(PSDeployment).FullName,
            Assert.Single(Assert.Single(typeof(GetManagementGroupDeploymentCmdlet).GetCustomAttributes<OutputTypeAttribute>()).Type).Name);
    }

    [Theory]
    [InlineData(typeof(NewManagementGroupDeploymentCmdlet))]
    [InlineData(typeof(TestManagementGroupDeploymentCmdlet))]
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
    [InlineData(typeof(GetManagementGroupDeploymentCmdlet), "GetByDeploymentName", new[] { "GetByDeploymentId", "GetByDeploymentName" })]
    [InlineData(typeof(StopManagementGroupDeploymentCmdlet), "StopByDeploymentName", new[] { "StopByDeploymentId", "StopByDeploymentName", "StopByInputObject" })]
    [InlineData(typeof(RemoveManagementGroupDeploymentCmdlet), "RemoveByDeploymentName", new[] { "RemoveByDeploymentId", "RemoveByDeploymentName", "RemoveByInputObject" })]
    public void NameCmdlets_UseLegacyParameterSetsAndAliases(Type cmdlet, string defaultSet, string[] sets)
    {
        Assert.Equal(defaultSet, cmdlet.GetCustomAttribute<CmdletAttribute>()!.DefaultParameterSetName);
        Assert.Equal(sets, ParameterSets(cmdlet));
        Assert.Equal(["DeploymentName"], Aliases(cmdlet, "Name"));
        Assert.Equal(["DeploymentId", "ResourceId"], Aliases(cmdlet, "Id"));
    }

    [Theory]
    [InlineData(typeof(NewManagementGroupDeploymentCmdlet), true)]
    [InlineData(typeof(RemoveManagementGroupDeploymentCmdlet), true)]
    [InlineData(typeof(StopManagementGroupDeploymentCmdlet), false)]
    [InlineData(typeof(TestManagementGroupDeploymentCmdlet), false)]
    [InlineData(typeof(GetManagementGroupDeploymentCmdlet), false)]
    public void AsJob_IsExposedOnlyWhereLegacyHasIt(Type cmdlet, bool expected)
    {
        Assert.Equal(expected, cmdlet.GetProperty("AsJob") is not null);
    }

    [Fact]
    public void NewCmdlet_ExposesLegacyWhatIfAndDeploymentParameters()
    {
        Type cmdlet = typeof(NewManagementGroupDeploymentCmdlet);

        Assert.Equal(["FullResourcePayloads", "ResourceIdOnly"], cmdlet.GetProperty("WhatIfResultFormat")!.GetCustomAttribute<ValidateSetAttribute>()!.ValidValues);
        Assert.Equal(
            ["RequestContent", "ResponseContent", "All", "None"],
            cmdlet.GetProperty("DeploymentDebugLogLevel")!.GetCustomAttribute<ArgumentCompletionsAttribute>()!.CompleteArgument("New-AzManagementGroupDeployment", "DeploymentDebugLogLevel", "", null!, null!).Select(c => c.CompletionText));
        Assert.NotNull(cmdlet.GetProperty("Tag"));
        Assert.NotNull(cmdlet.GetProperty("WhatIfExcludeChangeType"));
        Assert.NotNull(cmdlet.GetProperty("ProceedIfNoChange"));
        Assert.True(cmdlet.GetCustomAttribute<CmdletAttribute>()!.SupportsShouldProcess);
        Assert.NotNull(typeof(TestManagementGroupDeploymentCmdlet).GetProperty("SkipTemplateParameterPrompt"));
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
