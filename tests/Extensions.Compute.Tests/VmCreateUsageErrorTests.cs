// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

public class VmCreateUsageErrorTests
{
    [Theory]
    [InlineData("NotAType", null, null, "Security type must be one of")]
    [InlineData("ConfidentialVM", null, null, "OS disk security encryption type is required")]
    [InlineData("Standard", "VMGuestStateOnly", null, "can only be specified when security type is ConfidentialVM")]
    [InlineData("ConfidentialVM", "NotAnEncryptionType", null, "OS disk security encryption type must be one of")]
    [InlineData("ConfidentialVM", "VMGuestStateOnly", "des", "can only be specified when OS disk security encryption type is DiskWithVMGuestState")]
    public void SecurityProfileValidation_UsesUsageError(
        string? securityType,
        string? encryptionType,
        string? diskEncryptionSet,
        string expectedMessage)
    {
        var error = Assert.Throws<UsageErrorException>(() =>
            VmSecurityProfileResolver.Resolve(
                securityType,
                useTrustedLaunchDefaults: false,
                encryptionType,
                diskEncryptionSet,
                "sub",
                "rg"));

        Assert.Equal("UsageError", error.Code);
        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void DiskEncryptionSetResourceId_ValidatesExpectedResourceType()
    {
        const string publicIpId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Network/publicIPAddresses/not-a-des";

        var error = Assert.Throws<UsageErrorException>(() =>
            VmSecurityProfileResolver.Resolve(
                "ConfidentialVM",
                useTrustedLaunchDefaults: false,
                "DiskWithVMGuestState",
                publicIpId,
                "sub",
                "rg"));

        Assert.Equal("UsageError", error.Code);
        Assert.Contains("Invalid disk encryption set resource id", error.Message);
        Assert.Contains("Microsoft.Compute/diskEncryptionSets", error.Message);
    }

    [Fact]
    public void DiskEncryptionSetResourceId_PreservesValidId()
    {
        const string diskEncryptionSetId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/diskEncryptionSets/des";

        var profile = VmSecurityProfileResolver.Resolve(
            "ConfidentialVM",
            useTrustedLaunchDefaults: false,
            "DiskWithVMGuestState",
            diskEncryptionSetId,
            "other-sub",
            "other-rg");

        Assert.Equal(diskEncryptionSetId, profile.OsDiskSecureVMDiskEncryptionSetId);
    }

    [Theory]
    [InlineData("", "Image must not be empty")]
    [InlineData("UbuntuLTS", "Only marketplace image URNs")]
    public void ImageValidation_UsesUsageError(string image, string expectedMessage)
    {
        var error = Assert.Throws<UsageErrorException>(() =>
            ImageReferenceResolver.ResolveMarketplaceUrn(image));

        Assert.Equal("UsageError", error.Code);
        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void EmptySshKeyValue_UsesUsageError()
    {
        var error = Assert.Throws<UsageErrorException>(() =>
            OSProfileResolver.Resolve([" "], "azureuser", adminPassword: null));

        Assert.Equal("UsageError", error.Code);
        Assert.Contains("SSH public key value cannot be empty", error.Message);
    }
}
