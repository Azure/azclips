// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Azure.Azclips.Cmdlets.Compute;

namespace Cmdlets.Tests;

public class NewVMCmdletTests
{
    [Fact]
    public void NetworkInterfaceId_IsRequiredOnlyForExistingNicParameterSet()
    {
        var attributes = typeof(NewVMCmdlet)
            .GetProperty(nameof(NewVMCmdlet.NetworkInterfaceId))!
            .GetCustomAttributes(typeof(ParameterAttribute), inherit: true)
            .Cast<ParameterAttribute>()
            .ToList();

        var parameter = Assert.Single(attributes);
        Assert.Equal("ExistingNetworkInterface", parameter.ParameterSetName);
        Assert.True(parameter.Mandatory);
        Assert.Equal(typeof(string[]), typeof(NewVMCmdlet).GetProperty(nameof(NewVMCmdlet.NetworkInterfaceId))!.PropertyType);
    }

    [Fact]
    public void NetworkCreationParameters_AreExcludedFromExistingNicParameterSet()
    {
        foreach (string propertyName in new[]
        {
            nameof(NewVMCmdlet.NetworkInterfaceName),
            nameof(NewVMCmdlet.VirtualNetworkName),
            nameof(NewVMCmdlet.SubnetName),
            nameof(NewVMCmdlet.SubnetNames),
            nameof(NewVMCmdlet.SubnetAddressPrefixes),
            nameof(NewVMCmdlet.AddressPrefixes),
            nameof(NewVMCmdlet.SecurityGroupName),
            nameof(NewVMCmdlet.PublicIpAddressName),
            nameof(NewVMCmdlet.AcceleratedNetworking),
        })
        {
            var attributes = typeof(NewVMCmdlet)
                .GetProperty(propertyName)!
                .GetCustomAttributes(typeof(ParameterAttribute), inherit: true)
                .Cast<ParameterAttribute>();

            Assert.All(attributes, attribute => Assert.Equal("NewNetworkInterface", attribute.ParameterSetName));
        }
    }

    [Fact]
    public void NetworkInterfaceDeleteOption_AcceptsOnlyDeleteAndDetach()
    {
        var validateSet = Assert.Single(
            typeof(NewVMCmdlet)
                .GetProperty(nameof(NewVMCmdlet.NetworkInterfaceDeleteOption))!
                .GetCustomAttributes(typeof(ValidateSetAttribute), inherit: true)
                .Cast<ValidateSetAttribute>());

        Assert.True(validateSet.IgnoreCase);
        Assert.Equal(["Delete", "Detach"], validateSet.ValidValues);
    }
}
