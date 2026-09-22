extern alias CmdletsResources;

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ResourcesCmdlets = CmdletsResources::Microsoft.Azure.Azclips.Cmdlets.Resources;

namespace Cmdlets.Tests;

public class ManagementGroupDeploymentIdTests
{
    [Fact]
    public void Parse_ReturnsManagementGroupAndDeploymentNames()
    {
        (string managementGroupId, string name) = ResourcesCmdlets.ManagementGroupDeploymentId.Parse(
            "/providers/Microsoft.Management/managementGroups/test-mg/providers/Microsoft.Resources/deployments/test-deployment");

        Assert.Equal("test-mg", managementGroupId);
        Assert.Equal("test-deployment", name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/providers/Microsoft.Management/managementGroups/test-mg/providers/Microsoft.Resources/deployments/test-deployment/operations/1")]
    [InlineData("/providers/Microsoft.Management/managementGroups/test-mg/providers/Microsoft.Compute/deployments/test-deployment")]
    public void Parse_RejectsInvalidResourceIds(string id)
    {
        Assert.Throws<ArgumentException>(() => ResourcesCmdlets.ManagementGroupDeploymentId.Parse(id));
    }
}
