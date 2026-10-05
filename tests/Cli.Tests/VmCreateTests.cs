// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute;
using Xunit;

namespace Cli.Tests;

public class VmCreateTests
{
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
}
