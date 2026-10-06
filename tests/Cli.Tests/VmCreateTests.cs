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
}
