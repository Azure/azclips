// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;

namespace Extensions.ManagedServiceIdentity.Tests;

public class ManagedIdentityResolverTests
{
    private const string MissingAddressMessage = "Provide either --ids, or both --name and --resource-group.";

    [Theory]
    [InlineData(null, null)]
    [InlineData("id1", null)]
    [InlineData(null, "rg1")]
    [InlineData("", "rg1")]
    [InlineData("   ", "rg1")]
    [InlineData("id1", "")]
    [InlineData("id1", "   ")]
    public void ValidateInputs_MissingOrBlankNameOrResourceGroup_ThrowsInvalidInput(string? name, string? resourceGroup)
    {
        Exception? exception = Record.Exception(() => ManagedIdentityResolver.ValidateInputs(resourceGroup, name));

        Assert.Equal(MissingAddressMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public void ValidateInputs_NameAndResourceGroup_DoesNotThrow()
    {
        Exception? exception = Record.Exception(() => ManagedIdentityResolver.ValidateInputs("rg1", "id1"));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseResourceId_NullOrBlank_ThrowsInvalidInput(string? id)
    {
        Exception? exception = Record.Exception(() => ManagedIdentityResolver.ParseResourceId(id!));

        Assert.Equal("Identity resource id cannot be empty.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("not-a-resource-id")]
    [InlineData("/subscriptions/s/resourceGroups/rg")]
    [InlineData("/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mi/extra")]
    // Eight segments with exactly one wrong, so each segment check is pinned on its own.
    [InlineData("/subscription/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mi")]
    [InlineData("/subscriptions/s/resourceGroup/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mi")]
    [InlineData("/subscriptions/s/resourceGroups/rg/provider/Microsoft.ManagedIdentity/userAssignedIdentities/mi")]
    [InlineData("/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/userAssignedIdentities/mi")]
    [InlineData("/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/systemAssignedIdentities/mi")]
    public void ParseResourceId_InvalidFormat_ThrowsInvalidInput(string id)
    {
        Exception? exception = Record.Exception(() => ManagedIdentityResolver.ParseResourceId(id));

        Assert.Equal(InvalidIdMessage(id), Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData("/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mi")]
    [InlineData(" /SUBSCRIPTIONS/s/RESOURCEGROUPS/rg/providers/microsoft.managedidentity/USERASSIGNEDIDENTITIES/mi/ ")]
    public void ParseResourceId_ValidId_ReturnsParts(string id)
    {
        var parts = ManagedIdentityResolver.ParseResourceId(id);

        Assert.Equal(("s", "rg", "mi"), parts);
    }

    private string InvalidIdMessage(string id) =>
        $"Invalid managed identity resource id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{{resourceName}}.";
}
