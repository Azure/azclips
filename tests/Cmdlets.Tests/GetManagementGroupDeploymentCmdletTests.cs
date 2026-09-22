extern alias CmdletsResources;

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Reflection;
using ResourcesCmdlets = CmdletsResources::Microsoft.Azure.Azclips.Cmdlets.Resources;

namespace Cmdlets.Tests;

public class GetManagementGroupDeploymentCmdletTests
{
    private const string DeploymentId =
        "/providers/Microsoft.Management/managementGroups/test-mg/providers/Microsoft.Resources/deployments/test-deployment";

    [Fact]
    public void PowerShellSurface_DefinesParameterSetsAndAliases()
    {
        Type cmdlet = typeof(ResourcesCmdlets.GetManagementGroupDeploymentCmdlet);

        AssertParameterSet(cmdlet, "ManagementGroupId", "Get", mandatory: true);
        AssertParameterSet(cmdlet, "ManagementGroupId", "List", mandatory: true);
        AssertParameterSet(cmdlet, "Name", "Get", mandatory: true);
        AssertParameterSet(cmdlet, "Id", "Id", mandatory: true);

        AliasAttribute nameAliases = Assert.Single(
            cmdlet.GetProperty("Name")!.GetCustomAttributes<AliasAttribute>());
        Assert.Contains("DeploymentName", nameAliases.AliasNames);

        AliasAttribute idAliases = Assert.Single(
            cmdlet.GetProperty("Id")!.GetCustomAttributes<AliasAttribute>());
        Assert.Contains("DeploymentId", idAliases.AliasNames);
        Assert.Contains("ResourceId", idAliases.AliasNames);
    }

    [Fact]
    public void IdParameterSet_ParsesCanonicalIdAndDispatchesGet()
    {
        (string commandName, Dictionary<string, object> parameters) =
            ResourcesCmdlets.GetManagementGroupDeploymentCmdlet.BuildCommand(
                "Id", "", null, DeploymentId, "test-profile");

        Assert.Equal("Get-AzManagementGroupDeployment_Get", commandName);
        Assert.Equal("test-mg", parameters["ManagementGroupId"]);
        Assert.Equal("test-deployment", parameters["Name"]);
        Assert.Equal("test-profile", parameters["Profile"]);
    }

    [Fact]
    public void IdParameterSet_RejectsNonCanonicalIdBeforeDispatch()
    {
        Assert.Throws<ArgumentException>(() =>
            ResourcesCmdlets.GetManagementGroupDeploymentCmdlet.BuildCommand(
                "Id", "", null, DeploymentId + "/operations/1", null));
    }

    private static void AssertParameterSet(Type cmdlet, string propertyName, string parameterSet, bool mandatory)
    {
        ParameterAttribute parameter = Assert.Single(
            cmdlet.GetProperty(propertyName)!.GetCustomAttributes<ParameterAttribute>(),
            attribute => attribute.ParameterSetName == parameterSet);
        Assert.Equal(mandatory, parameter.Mandatory);
    }
}
