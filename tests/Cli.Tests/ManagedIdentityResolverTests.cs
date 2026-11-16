// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;

namespace Cli.Tests;

public class ManagedIdentityResolverTests
{
    private const string MissingAddressMessage = "Provide either --ids, or both --name and --resource-group.";

    private static string InvalidIdMessage(string id) =>
        $"Invalid managed identity resource id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{{resourceName}}.";

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
        var ex = Assert.Throws<InvalidInputException>(() => ManagedIdentityResolver.ValidateInputs(resourceGroup, name));

        Assert.Equal(MissingAddressMessage, ex.Message);
    }

    [Fact]
    public void ValidateInputs_NameAndResourceGroup_DoesNotThrow() =>
        Assert.Null(Record.Exception(() => ManagedIdentityResolver.ValidateInputs("rg1", "id1")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseResourceId_NullOrBlank_ThrowsInvalidInput(string? id)
    {
        var ex = Assert.Throws<InvalidInputException>(() => ManagedIdentityResolver.ParseResourceId(id!));

        Assert.Equal("Identity resource id cannot be empty.", ex.Message);
    }

    [Theory]
    [InlineData("not-a-resource-id")]
    [InlineData("/subscriptions/s/resourceGroups/rg")]
    [InlineData("/subscriptions/s/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm")]
    [InlineData("/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mi/extra")]
    public void ParseResourceId_InvalidFormat_ThrowsInvalidInput(string id)
    {
        var ex = Assert.Throws<InvalidInputException>(() => ManagedIdentityResolver.ParseResourceId(id));

        Assert.Equal(InvalidIdMessage(id), ex.Message);
    }

    [Theory]
    [InlineData("/subscriptions/s/resourceGroups/rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mi")]
    [InlineData(" /SUBSCRIPTIONS/s/RESOURCEGROUPS/rg/providers/microsoft.managedidentity/USERASSIGNEDIDENTITIES/mi/ ")]
    public void ParseResourceId_ValidId_ReturnsParts(string id) =>
        Assert.Equal(("s", "rg", "mi"), ManagedIdentityResolver.ParseResourceId(id));
}
