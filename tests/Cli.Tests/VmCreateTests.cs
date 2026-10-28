// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli;
using Core.Cli.Parser;
using Core.Runtime;
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

    [Fact]
    public void NewAzVM_ExposesTagsParameter()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("New-AzVM", ClientType.PowerShell);

        Assert.True(metadata.Parameters.TryGetValue("Tags", out var parameter));
        Assert.Equal("TagsInput", parameter.Name);
        Assert.Equal(typeof(string[]), parameter.ParameterType);
        Assert.False(parameter.IsRequired);
    }

    [Fact]
    public void VmCreate_ExposesNetworkSelectionParameters()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("vm create", ClientType.CLI);

        Assert.Equal(typeof(string), metadata.Parameters["subnet"].ParameterType);
        Assert.Equal("SubnetName", metadata.Parameters["subnet"].Name);
        Assert.Equal(typeof(List<string>), metadata.Parameters["nics"].ParameterType);
        Assert.Equal("NetworkInterfaceIds", metadata.Parameters["nics"].Name);
        Assert.Equal(typeof(List<string>), metadata.Parameters["nic-delete-option"].ParameterType);
        Assert.Equal("NicDeleteOptions", metadata.Parameters["nic-delete-option"].Name);
        Assert.False(metadata.Parameters["network-interface-name"].IsRequired);
        Assert.False(metadata.Parameters["virtual-network-name"].IsRequired);
    }

    [Fact]
    public void VmCreate_ExposesSingleZoneParameter()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("vm create", ClientType.CLI);

        Assert.True(metadata.Parameters.TryGetValue("zone", out var parameter));
        Assert.Equal("Zone", parameter.Name);
        Assert.Equal(typeof(string), parameter.ParameterType);
        Assert.False(parameter.IsRequired);
        Assert.Empty(parameter.AllowedValues);
        Assert.Equal("zone", metadata.ParameterAliases["z"]);
    }

    [Fact]
    public void NewAzVM_ExposesNetworkSelectionParameters()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("New-AzVM", ClientType.PowerShell);

        Assert.Equal(typeof(string), metadata.Parameters["SubnetName"].ParameterType);
        Assert.Equal("SubnetName", metadata.Parameters["SubnetName"].Name);
        Assert.Equal(typeof(List<string>), metadata.Parameters["NetworkInterfaceId"].ParameterType);
        Assert.Equal("NetworkInterfaceIds", metadata.Parameters["NetworkInterfaceId"].Name);
        Assert.Equal(typeof(string), metadata.Parameters["NetworkInterfaceDeleteOption"].ParameterType);
        Assert.Equal(["Delete", "Detach"], metadata.Parameters["NetworkInterfaceDeleteOption"].AllowedValues);
        Assert.False(metadata.Parameters["NetworkInterfaceName"].IsRequired);
        Assert.False(metadata.Parameters["VirtualNetworkName"].IsRequired);
    }

    [Fact]
    public void NewAzVM_ExposesSingleZoneParameter()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("New-AzVM", ClientType.PowerShell);

        Assert.True(metadata.Parameters.TryGetValue("Zone", out var parameter));
        Assert.Equal("Zone", parameter.Name);
        Assert.Equal(typeof(string), parameter.ParameterType);
        Assert.False(parameter.IsRequired);
        Assert.Empty(parameter.AllowedValues);
    }

    [Fact]
    public void VmCreate_RejectsMultipleZoneValuesInSingleOccurrence()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("vm create", ClientType.CLI);
        var (_, args) = new ExtensionParser().Parse(["vm", "create", "--zone", "1", "2"]);

        var ex = Assert.Throws<UsageErrorException>(() => CreateCliProvider().ParseArguments(args, metadata.Parameters, metadata.ParameterAliases));

        Assert.Contains("Parameter 'zone' accepts a single value.", ex.Message);
    }

    [Fact]
    public void VmCreate_RejectsRepeatedZoneOccurrenceViaAlias()
    {
        var metadata = new ExtensionIndex().GetCommandMetadata("vm create", ClientType.CLI);
        var (_, args) = new ExtensionParser().Parse(["vm", "create", "-z", "1", "-z", "2"]);

        var ex = Assert.Throws<UsageErrorException>(() => CreateCliProvider().ParseArguments(args, metadata.Parameters, metadata.ParameterAliases));

        Assert.Contains("Parameter 'z' accepts a single value and cannot be specified multiple times.", ex.Message);
    }

    private static CLIArgumentParsingProvider CreateCliProvider()
    {
        var logging = new ConsoleLoggingService();
        return new CLIArgumentParsingProvider(new PropertyParser(logging), logging);
    }
}
