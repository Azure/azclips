// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Extensions.Resources.Tests;

/// <summary>Direct tests for resource group id parsing in <see cref="ResourceGroupIdResolver"/>.</summary>
public class ResourceGroupIdResolverTests
{
    // Not reachable through SetResourceGroupProcessor_Orchestrator, which treats a blank Id as "no address".
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_BlankId_ThrowsInvalidInput(string? id)
    {
        Exception? exception = Record.Exception(() => ResourceGroupIdResolver.Parse(id!));

        Assert.Equal("Resource group id cannot be empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("/subscriptions/sub-1/resourceGroups")]
    [InlineData("/subscriptions/sub-1/resourceGroups/rg/extra")]
    [InlineData("/subscription/sub-1/resourceGroups/rg")]
    [InlineData("/subscriptions/sub-1/resourceGroup/rg")]
    public void Parse_MalformedId_ThrowsInvalidInput(string id)
    {
        Exception? exception = Record.Exception(() => ResourceGroupIdResolver.Parse(id));

        Assert.Equal(
            $"Invalid resource group id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("/subscriptions/sub-1/resourceGroups/rg")]
    [InlineData(" SUBSCRIPTIONS/sub-1/RESOURCEGROUPS/rg/ ")]
    public void Parse_ValidId_ReturnsSubscriptionAndResourceGroup(string id)
    {
        var parts = ResourceGroupIdResolver.Parse(id);

        Assert.Equal(("sub-1", "rg"), parts);
    }
}
