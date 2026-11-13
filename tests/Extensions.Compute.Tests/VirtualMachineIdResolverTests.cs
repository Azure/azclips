// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

public class VirtualMachineIdResolverTests
{
    private const string VmId = "/subscriptions/s/resourceGroups/r/providers/Microsoft.Compute/virtualMachines/v";

    [Fact]
    public void Ids_Only_ReturnsTrue()
    {
        Assert.True(VirtualMachineIdResolver.HasIds([VmId], null, null));
    }

    [Fact]
    public void NameAndResourceGroup_ReturnsFalse()
    {
        Assert.False(VirtualMachineIdResolver.HasIds(null, "vm", "rg"));
        Assert.False(VirtualMachineIdResolver.HasIds([], "vm", "rg"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("vm", null)]
    [InlineData(null, "rg")]
    [InlineData("", "rg")]
    [InlineData("vm", "  ")]
    public void MissingTarget_Throws(string? name, string? resourceGroup)
    {
        var ex = Assert.ThrowsAny<AzclipsErrorException>(() => VirtualMachineIdResolver.HasIds(null, name, resourceGroup));

        Assert.Contains("identifier is required", ex.Message);
    }

    [Theory]
    [InlineData("vm", null)]
    [InlineData(null, "rg")]
    [InlineData("vm", "rg")]
    public void IdsWithNameOrResourceGroup_Throws(string? name, string? resourceGroup)
    {
        var ex = Assert.ThrowsAny<AzclipsErrorException>(() => VirtualMachineIdResolver.HasIds([VmId], name, resourceGroup));

        Assert.Contains("cannot be combined", ex.Message);
    }
}
