// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

public class VmSecurityProfileResolverTests
{
    [Fact]
    public void OmittedSecurityType_UsesTrustedLaunchDefaultsWhenImageSupportsIt()
    {
        var profile = VmSecurityProfileResolver.Resolve(
            requestedSecurityType: null,
            useTrustedLaunchDefaults: true,
            osDiskSecurityEncryptionType: null,
            osDiskSecureVMDiskEncryptionSet: null,
            "sub",
            "rg");

        Assert.Equal("TrustedLaunch", profile.SecurityType);
        Assert.True(profile.SecureBootEnabled);
        Assert.True(profile.VTpmEnabled);
        Assert.Null(profile.OsDiskSecurityEncryptionType);
        Assert.Null(profile.OsDiskSecureVMDiskEncryptionSetId);
    }

    [Fact]
    public void OmittedSecurityType_LeavesSecurityProfileUnsetWhenImageDoesNotRequestDefaults()
    {
        var profile = VmSecurityProfileResolver.Resolve(
            requestedSecurityType: null,
            useTrustedLaunchDefaults: false,
            osDiskSecurityEncryptionType: null,
            osDiskSecureVMDiskEncryptionSet: null,
            "sub",
            "rg");

        Assert.Null(profile.SecurityType);
        Assert.Null(profile.SecureBootEnabled);
        Assert.Null(profile.VTpmEnabled);
    }

    [Theory]
    [InlineData("TrustedLaunch")]
    [InlineData("ConfidentialVM")]
    public void TrustedLaunchAndConfidentialVm_EnableUefiSettings(string securityType)
    {
        var encryptionType = securityType == "ConfidentialVM" ? "VMGuestStateOnly" : null;

        var profile = VmSecurityProfileResolver.Resolve(
            securityType,
            useTrustedLaunchDefaults: false,
            encryptionType,
            osDiskSecureVMDiskEncryptionSet: null,
            "sub",
            "rg");

        Assert.Equal(securityType, profile.SecurityType);
        Assert.True(profile.SecureBootEnabled);
        Assert.True(profile.VTpmEnabled);
    }

    [Fact]
    public void Standard_DisablesUefiSettings()
    {
        var profile = VmSecurityProfileResolver.Resolve(
            "Standard",
            useTrustedLaunchDefaults: true,
            osDiskSecurityEncryptionType: null,
            osDiskSecureVMDiskEncryptionSet: null,
            "sub",
            "rg");

        Assert.Equal("Standard", profile.SecurityType);
        Assert.Null(profile.SecureBootEnabled);
        Assert.Null(profile.VTpmEnabled);
    }

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

    [Fact]
    public void DiskEncryptionSetName_ExpandsWithinCurrentResourceGroup()
    {
        var profile = VmSecurityProfileResolver.Resolve(
            "ConfidentialVM",
            useTrustedLaunchDefaults: false,
            "DiskWithVMGuestState",
            "des",
            "sub",
            "rg");

        Assert.Equal(
            "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/diskEncryptionSets/des",
            profile.OsDiskSecureVMDiskEncryptionSetId);
    }
}
