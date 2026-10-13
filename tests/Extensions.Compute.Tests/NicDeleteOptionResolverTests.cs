// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

public class NicDeleteOptionResolverTests
{
    [Fact]
    public void ResolveForNewNic_EmptyCliValues_ReturnsNull()
    {
        var result = NicDeleteOptionResolver.ResolveForNewNic([], null);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("Delete", "Delete")]
    [InlineData("delete", "Delete")]
    [InlineData("Detach", "Detach")]
    [InlineData("detach", "Detach")]
    public void ResolveForNewNic_CliSingleValue_Normalizes(string input, string expected)
    {
        var result = NicDeleteOptionResolver.ResolveForNewNic([input], null);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Delete", "Delete")]
    [InlineData("Detach", "Detach")]
    public void ResolveForNewNic_PowerShellValue_Normalizes(string input, string expected)
    {
        var result = NicDeleteOptionResolver.ResolveForNewNic([], input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveForNewNic_BothSurfacesSpecified_ThrowsUsageError()
    {
        var ex = Assert.Throws<UsageErrorException>(
            () => NicDeleteOptionResolver.ResolveForNewNic(["Delete"], "Detach"));
        Assert.Contains("Specify only one network interface delete option parameter.", ex.Message);
    }

    [Fact]
    public void ResolveForNewNic_AssignmentSyntax_ThrowsUsageError()
    {
        var ex = Assert.Throws<UsageErrorException>(
            () => NicDeleteOptionResolver.ResolveForNewNic(["nic1=Delete"], null));
        Assert.Contains("must be a single value", ex.Message);
    }

    [Fact]
    public void ResolveForNewNic_MultipleValues_ThrowsUsageError()
    {
        var ex = Assert.Throws<UsageErrorException>(
            () => NicDeleteOptionResolver.ResolveForNewNic(["Delete", "Detach"], null));
        Assert.Contains("must be a single value", ex.Message);
    }

    [Fact]
    public void ResolveForExistingNics_EmptyCliValues_ReturnsNulls()
    {
        var result = NicDeleteOptionResolver.ResolveForExistingNics(["nic1", "nic2"], [], null);
        Assert.Equal([null, null], result);
    }

    [Fact]
    public void ResolveForExistingNics_PowerShellValue_AppliesToAll()
    {
        var result = NicDeleteOptionResolver.ResolveForExistingNics(["nic1", "nic2"], [], "Delete");
        Assert.Equal(["Delete", "Delete"], result);
    }

    [Fact]
    public void ResolveForExistingNics_SharedCliValue_AppliesToAll()
    {
        var result = NicDeleteOptionResolver.ResolveForExistingNics(["nic1", "nic2"], ["Detach"], null);
        Assert.Equal(["Detach", "Detach"], result);
    }

    [Fact]
    public void ResolveForExistingNics_AssignmentsByShortName()
    {
        var result = NicDeleteOptionResolver.ResolveForExistingNics(
            ["nic1", "nic2"], ["nic1=Delete", "nic2=Detach"], null);
        Assert.Equal(["Delete", "Detach"], result);
    }

    [Fact]
    public void ResolveForExistingNics_AssignmentsByTerminalNameForResourceIds()
    {
        string nicId1 = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic1";
        string nicId2 = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic2";
        var result = NicDeleteOptionResolver.ResolveForExistingNics(
            [nicId1, nicId2], ["nic1=Delete", "nic2=Detach"], null);
        Assert.Equal(["Delete", "Detach"], result);
    }

    [Fact]
    public void ResolveForExistingNics_PartialAssignments_LeavesUnassignedAsNull()
    {
        string nicId1 = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic1";
        string nicId2 = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic2";
        var result = NicDeleteOptionResolver.ResolveForExistingNics(
            [nicId1, nicId2], ["nic2=Delete"], null);
        Assert.Equal([null, "Delete"], result);
    }

    [Fact]
    public void ResolveForExistingNics_AmbiguousTerminalName_ThrowsUsageError()
    {
        string nicId1 = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Network/networkInterfaces/nic1";
        string nicId2 = "/subscriptions/sub2/resourceGroups/rg2/providers/Microsoft.Network/networkInterfaces/nic1";
        var ex = Assert.Throws<UsageErrorException>(
            () => NicDeleteOptionResolver.ResolveForExistingNics([nicId1, nicId2], ["nic1=Delete"], null));
        Assert.Contains("ambiguous", ex.Message);
    }

    [Fact]
    public void ResolveForExistingNics_FullIdDisambiguatesSameTerminalName()
    {
        string nicId1 = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Network/networkInterfaces/nic1";
        string nicId2 = "/subscriptions/sub2/resourceGroups/rg2/providers/Microsoft.Network/networkInterfaces/nic1";
        var result = NicDeleteOptionResolver.ResolveForExistingNics(
            [nicId1, nicId2], [$"{nicId1}=Delete", $"{nicId2}=Detach"], null);
        Assert.Equal(["Delete", "Detach"], result);
    }

    [Fact]
    public void ResolveForExistingNics_DuplicateOptionForSameNic_ThrowsUsageError()
    {
        string nicId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Network/networkInterfaces/nic1";
        var ex = Assert.Throws<UsageErrorException>(
            () => NicDeleteOptionResolver.ResolveForExistingNics([nicId], ["nic1=Delete", $"{nicId}=Detach"], null));
        Assert.Contains("specified more than once", ex.Message);
    }

    [Fact]
    public void ResolveForExistingNics_UnknownTarget_ThrowsUsageError()
    {
        var ex = Assert.Throws<UsageErrorException>(
            () => NicDeleteOptionResolver.ResolveForExistingNics(["nic1"], ["nic2=Delete"], null));
        Assert.Contains("was not supplied through --nics", ex.Message);
    }

    [Fact]
    public void ResolveForExistingNics_InvalidValue_ThrowsUsageError()
    {
        var ex = Assert.Throws<UsageErrorException>(
            () => NicDeleteOptionResolver.ResolveForExistingNics(["nic1"], ["nic1=InvalidOption"], null));
        Assert.Contains("Network interface delete option must be one of: Delete, Detach", ex.Message);
    }
}
