// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Extensions.Resources.Tests;

/// <summary>Direct tests for deployment id parsing in <see cref="DeploymentIdResolver"/>.</summary>
public class DeploymentIdResolverTests
{
    private const string ResourceGroupDeploymentId =
        "/subscriptions/sub-1/resourceGroups/rg/providers/Microsoft.Resources/deployments/dep";

    private const string ManagementGroupDeploymentId =
        "/providers/Microsoft.Management/managementGroups/mg1/providers/Microsoft.Resources/deployments/dep";

    // Current behaviour: malformed ids surface as InvalidOperationException rather than InvalidInputException.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/subscriptions/sub-1/resourceGroups/rg/providers/Microsoft.Resources/deployments")]
    [InlineData(ResourceGroupDeploymentId + "/extra")]
    [InlineData("/subscriptions/sub-1/resourceGroups/rg/providers/Microsoft.Compute/deployments/dep")]
    public void Parse_MalformedId_ThrowsInvalidOperation(string id)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DeploymentIdResolver.Parse(id));

        Assert.Equal(
            $"Invalid deployment id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/Microsoft.Resources/deployments/{{deploymentName}}.",
            ex.Message);
    }

    [Theory]
    [InlineData(ResourceGroupDeploymentId)]
    [InlineData(" SUBSCRIPTIONS/sub-1/RESOURCEGROUPS/rg/PROVIDERS/microsoft.resources/DEPLOYMENTS/dep/ ")]
    public void Parse_ValidId_ReturnsParts(string id)
    {
        Assert.Equal(("sub-1", "rg", "dep"), DeploymentIdResolver.Parse(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/providers/Microsoft.Management/managementGroups/mg1/providers/Microsoft.Resources/deployments")]
    [InlineData(ManagementGroupDeploymentId + "/extra")]
    [InlineData(ResourceGroupDeploymentId)]
    public void ParseManagementGroup_MalformedId_ThrowsInvalidOperation(string id)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DeploymentIdResolver.ParseManagementGroup(id));

        Assert.Equal(
            $"Invalid deployment id '{id}'. Expected /providers/Microsoft.Management/managementGroups/{{managementGroupId}}/providers/Microsoft.Resources/deployments/{{deploymentName}}.",
            ex.Message);
    }

    [Theory]
    [InlineData(ManagementGroupDeploymentId)]
    [InlineData(" PROVIDERS/microsoft.management/MANAGEMENTGROUPS/mg1/providers/Microsoft.Resources/deployments/dep/ ")]
    public void ParseManagementGroup_ValidId_ReturnsParts(string id)
    {
        Assert.Equal(("mg1", "dep"), DeploymentIdResolver.ParseManagementGroup(id));
    }
}
