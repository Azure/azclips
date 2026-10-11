// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute;
using Xunit;

namespace Cli.Tests;

public class VmCreateTests
{
    [Fact]
    public void VmCreate_ExposesSshKeyValuesParameter()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("vm create", ClientType.CLI);

        Assert.True(metadata.Parameters.TryGetValue("ssh-key-values", out var parameter));
        Assert.Equal("SshKeyValues", parameter.Name);
        Assert.Equal(typeof(List<string>), parameter.ParameterType);
        Assert.False(parameter.IsRequired);
    }

    [Fact]
    public void VmCreate_ExposesImageParameter()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("vm create", ClientType.CLI);

        Assert.True(metadata.Parameters.TryGetValue("image", out var parameter));
        Assert.Equal("Image", parameter.Name);
        Assert.Equal(typeof(string), parameter.ParameterType);
        Assert.False(parameter.IsRequired);
    }

    [Fact]
    public void VmCreate_ExposesSecurityTypeParameter()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("vm create", ClientType.CLI);

        Assert.True(metadata.Parameters.TryGetValue("security-type", out var parameter));
        Assert.Equal("SecurityType", parameter.Name);
        Assert.Equal(typeof(string), parameter.ParameterType);
        Assert.False(parameter.IsRequired);
        Assert.Contains("TrustedLaunch", parameter.AllowedValues);
        Assert.Contains("ConfidentialVM", parameter.AllowedValues);
        Assert.Contains("Standard", parameter.AllowedValues);
    }

    [Fact]
    public void VmCreate_ExposesOsDiskSecurityEncryptionParameters()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("vm create", ClientType.CLI);

        Assert.True(metadata.Parameters.TryGetValue("os-disk-security-encryption-type", out var encryptionType));
        Assert.Equal("OsDiskSecurityEncryptionType", encryptionType.Name);
        Assert.Equal(typeof(string), encryptionType.ParameterType);
        Assert.False(encryptionType.IsRequired);
        Assert.Contains("VMGuestStateOnly", encryptionType.AllowedValues);
        Assert.Contains("DiskWithVMGuestState", encryptionType.AllowedValues);
        Assert.Contains("NonPersistedTPM", encryptionType.AllowedValues);

        Assert.True(metadata.Parameters.TryGetValue("os-disk-secure-vm-disk-encryption-set", out var encryptionSet));
        Assert.Equal("OsDiskSecureVMDiskEncryptionSet", encryptionSet.Name);
        Assert.Equal(typeof(string), encryptionSet.ParameterType);
        Assert.False(encryptionSet.IsRequired);
    }

    [Fact]
    public void VmCreate_ExposesTagsParameter()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("vm create", ClientType.CLI);

        Assert.True(metadata.Parameters.TryGetValue("tags", out var parameter));
        Assert.Equal("TagsInput", parameter.Name);
        Assert.Equal(typeof(string[]), parameter.ParameterType);
        Assert.False(parameter.IsRequired);
    }

    [Fact]
    public void NewAzVM_ExposesSecurityTypeParameter()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("New-AzVM", ClientType.PowerShell);

        Assert.True(metadata.Parameters.TryGetValue("SecurityType", out var parameter));
        Assert.Equal("SecurityType", parameter.Name);
        Assert.Equal(typeof(string), parameter.ParameterType);
        Assert.False(parameter.IsRequired);
        Assert.Contains("TrustedLaunch", parameter.AllowedValues);
        Assert.Contains("ConfidentialVM", parameter.AllowedValues);
        Assert.Contains("Standard", parameter.AllowedValues);
    }

    [Fact]
    public void NewAzVM_ExposesOsDiskSecurityEncryptionParameters()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("New-AzVM", ClientType.PowerShell);

        Assert.True(metadata.Parameters.TryGetValue("OSDiskSecurityEncryptionType", out var encryptionType));
        Assert.Equal("OsDiskSecurityEncryptionType", encryptionType.Name);
        Assert.Equal(typeof(string), encryptionType.ParameterType);
        Assert.False(encryptionType.IsRequired);
        Assert.Contains("VMGuestStateOnly", encryptionType.AllowedValues);
        Assert.Contains("DiskWithVMGuestState", encryptionType.AllowedValues);
        Assert.Contains("NonPersistedTPM", encryptionType.AllowedValues);

        Assert.True(metadata.Parameters.TryGetValue("OSDiskSecureVMDiskEncryptionSet", out var encryptionSet));
        Assert.Equal("OsDiskSecureVMDiskEncryptionSet", encryptionSet.Name);
        Assert.Equal(typeof(string), encryptionSet.ParameterType);
        Assert.False(encryptionSet.IsRequired);
    }
}
