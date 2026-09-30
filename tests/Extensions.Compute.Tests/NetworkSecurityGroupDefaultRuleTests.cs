// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Microsoft.Azure.Azclips.Extensions.Compute.Processors;

namespace Extensions.Compute.Tests;

/// <summary>
/// Unit tests for <c>NetworkSecurityGroupDefaultRule</c>: the inbound rule legacy Azure CLI gives a
/// network security group that vm create creates (azure-cli <c>custom.py</c> and
/// <c>_template_builder.py</c> <c>build_nsg_resource</c>). vm create only deploys Windows today, so
/// the Linux rule is pinned here rather than through a command.
/// </summary>
public class NetworkSecurityGroupDefaultRuleTests
{
    [Theory]
    [InlineData("Windows")]
    [InlineData("windows")]
    [InlineData("WINDOWS")]
    public void Windows_AllowsRdp(string osType)
    {
        SecurityRule rule = NetworkSecurityGroupDefaultRule.For(osType);

        Assert.Equal("rdp", rule.Name);
        Assert.Equal("3389", rule.Properties.DestinationPortRange);
        AssertInboundTcpAllowedFromAnywhere(rule.Properties);
    }

    [Theory]
    [InlineData("Linux")]
    [InlineData("linux")]
    public void Linux_AllowsSsh(string osType)
    {
        SecurityRule rule = NetworkSecurityGroupDefaultRule.For(osType);

        Assert.Equal("default-allow-ssh", rule.Name);
        Assert.Equal("22", rule.Properties.DestinationPortRange);
        AssertInboundTcpAllowedFromAnywhere(rule.Properties);
    }

    private static void AssertInboundTcpAllowedFromAnywhere(SecurityRuleProperties properties)
    {
        Assert.Equal("Inbound", properties.Direction);
        Assert.Equal("Tcp", properties.Protocol);
        Assert.Equal("Allow", properties.Access);
        Assert.Equal(1000, properties.Priority);
        Assert.Equal("*", properties.SourceAddressPrefix);
        Assert.Equal("*", properties.SourcePortRange);
        Assert.Equal("*", properties.DestinationAddressPrefix);
    }
}
