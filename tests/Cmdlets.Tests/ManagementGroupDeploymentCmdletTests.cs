// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Reflection;
using Microsoft.Azure.Azclips.Cmdlets.Resources;

namespace Cmdlets.Tests;

public class ManagementGroupDeploymentCmdletTests
{
    private static readonly Type[] CmdletTypes =
    [
        typeof(NewManagementGroupDeploymentCmdlet),
        typeof(GetManagementGroupDeploymentCmdlet),
        typeof(StopManagementGroupDeploymentCmdlet),
        typeof(RemoveManagementGroupDeploymentCmdlet),
        typeof(TestManagementGroupDeploymentCmdlet),
    ];

    [Fact]
    public void ParseId_AcceptsManagementGroupDeploymentId()
    {
        const string id =
            "/providers/Microsoft.Management/managementGroups/myManagementGroup/"
            + "providers/Microsoft.Resources/deployments/myDeployment";

        (string managementGroupId, string name) = ParseId(id);

        Assert.Equal("myManagementGroup", managementGroupId);
        Assert.Equal("myDeployment", name);
    }

    [Theory]
    [InlineData("/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg/providers/Microsoft.Resources/deployments/deployment")]
    [InlineData("/providers/Microsoft.Management/managementGroups/mg/providers/Microsoft.Resources/deployments")]
    [InlineData("/providers/Microsoft.Management/managementGroups/mg/providers/Microsoft.Authorization/deployments/deployment")]
    public void ParseId_RejectsNonManagementGroupDeploymentIds(string id)
    {
        TargetInvocationException exception =
            Assert.Throws<TargetInvocationException>(() => ParseId(id));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    [Fact]
    public void StopAndRemove_InputObjectParametersAcceptPipelineInput()
    {
        foreach (Type cmdletType in new[]
                 {
                     typeof(StopManagementGroupDeploymentCmdlet),
                     typeof(RemoveManagementGroupDeploymentCmdlet),
                 })
        {
            ParameterAttribute parameter = Assert.Single(
                cmdletType.GetProperty("InputObject")!
                    .GetCustomAttributes<ParameterAttribute>());

            Assert.True(parameter.Mandatory);
            Assert.True(parameter.ValueFromPipeline);
            Assert.EndsWith("ByInputObject", parameter.ParameterSetName);
        }
    }

    [Fact]
    public void GetStopAndRemove_ExposeNameAndIdParameterSets()
    {
        AssertParameterSet<GetManagementGroupDeploymentCmdlet>("ManagementGroupId", "GetByName");
        AssertParameterSet<GetManagementGroupDeploymentCmdlet>("Id", "GetById");
        AssertParameterSet<StopManagementGroupDeploymentCmdlet>("ManagementGroupId", "StopByName");
        AssertParameterSet<StopManagementGroupDeploymentCmdlet>("Id", "StopById");
        AssertParameterSet<RemoveManagementGroupDeploymentCmdlet>("ManagementGroupId", "RemoveByName");
        AssertParameterSet<RemoveManagementGroupDeploymentCmdlet>("Id", "RemoveById");
    }

    [Fact]
    public void ManagementGroupDeploymentCmdlets_DoNotExposeAsJob()
    {
        foreach (Type cmdletType in CmdletTypes)
        {
            Assert.Null(cmdletType.GetProperty("AsJob"));
        }
    }

    [Fact]
    public void CreateAndDestructiveCmdlets_SupportShouldProcess()
    {
        foreach (Type cmdletType in new[]
                 {
                     typeof(NewManagementGroupDeploymentCmdlet),
                     typeof(StopManagementGroupDeploymentCmdlet),
                     typeof(RemoveManagementGroupDeploymentCmdlet),
                 })
        {
            CmdletAttribute attribute =
                Assert.Single(cmdletType.GetCustomAttributes<CmdletAttribute>());

            Assert.True(attribute.SupportsShouldProcess);
        }
    }

    [Fact]
    public void NewCmdlet_DeclaresCreateAndWhatIfOutputTypes()
    {
        OutputTypeAttribute attribute = Assert.Single(
            typeof(NewManagementGroupDeploymentCmdlet)
                .GetCustomAttributes<OutputTypeAttribute>());

        Assert.Equal(
            [
                "Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended",
                "Microsoft.Azure.Azclips.Extensions.Resources.Models.WhatIfOperationResult",
            ],
            attribute.Type.Select(type => type.Name));
    }

    private static (string ManagementGroupId, string Name) ParseId(string id)
    {
        Type helpers = typeof(GetManagementGroupDeploymentCmdlet).Assembly.GetType(
            "Microsoft.Azure.Azclips.Cmdlets.Resources.ManagementGroupDeploymentCmdletHelpers",
            throwOnError: true)!;
        MethodInfo method = helpers.GetMethod(
            "ParseId",
            BindingFlags.Public | BindingFlags.Static)!;

        return ((string ManagementGroupId, string Name))method.Invoke(null, [id])!;
    }

    private static void AssertParameterSet<TCmdlet>(string propertyName, string parameterSetName)
    {
        ParameterAttribute parameter = Assert.Single(
            typeof(TCmdlet).GetProperty(propertyName)!
                .GetCustomAttributes<ParameterAttribute>());

        Assert.Equal(parameterSetName, parameter.ParameterSetName);
    }
}
