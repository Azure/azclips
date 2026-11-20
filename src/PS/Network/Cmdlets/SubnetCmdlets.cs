// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

/// <summary>Inputs shared by subnet create and update.</summary>
public abstract class SubnetWriteCmdletBase : NetworkWriteCmdletBase
{
    [Parameter(Mandatory = true)]
    [Alias("ResourceName")]
    public string Name { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string VirtualNetworkName { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = string.Empty;

    [Parameter]
    [Alias("NetworkSecurityGroupId")]
    public string? NetworkSecurityGroup { get; set; }

    [Parameter]
    [Alias("RouteTableId")]
    public string? RouteTable { get; set; }

    [Parameter]
    [Alias("NatGatewayId")]
    public string? NatGateway { get; set; }

    [Parameter]
    [Alias("ServiceEndpoint")]
    public string[]? ServiceEndpoints { get; set; }

    [Parameter]
    [Alias("Delegation")]
    public string[]? Delegations { get; set; }

    [Parameter]
    [ValidateSet("Disabled", "Enabled", "NetworkSecurityGroupEnabled", "RouteTableEnabled")]
    public string? PrivateEndpointNetworkPolicies { get; set; }

    [Parameter]
    [ValidateSet("Disabled", "Enabled")]
    public string? PrivateLinkServiceNetworkPolicies { get; set; }

    [Parameter]
    public bool? DefaultOutboundAccess { get; set; }

    protected override string Target => $"Subnet '{Name}' in virtual network '{VirtualNetworkName}'";
}

[Cmdlet(VerbsCommon.New, "AzVirtualNetworkSubnet", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.Subnet")]
public class NewVirtualNetworkSubnetCmdlet : SubnetWriteCmdletBase
{
    [Parameter(Mandatory = true)]
    [Alias("AddressPrefix")]
    public string[] AddressPrefixes { get; set; } = [];

    protected override string Command => "New-AzVirtualNetworkSubnet";
}

[Cmdlet(VerbsCommon.Set, "AzVirtualNetworkSubnet", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.Subnet")]
public class SetVirtualNetworkSubnetCmdlet : SubnetWriteCmdletBase
{
    [Parameter]
    [Alias("AddressPrefix")]
    public string[]? AddressPrefixes { get; set; }

    protected override string Command => "Set-AzVirtualNetworkSubnet";

    protected override string Action => "Update";
}

[Cmdlet(VerbsCommon.Get, "AzVirtualNetworkSubnet", DefaultParameterSetName = ListSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.Subnet")]
public class GetVirtualNetworkSubnetCmdlet : NetworkGetCmdletBase
{
    [Parameter(ParameterSetName = GetSet, Mandatory = true)]
    [Alias("ResourceName")]
    public string? Name { get; set; }

    [Parameter(Mandatory = true)]
    public string VirtualNetworkName { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = string.Empty;

    [Parameter(ParameterSetName = GetSet)]
    public string? ExpandResource { get; set; }

    protected override string Noun => "AzVirtualNetworkSubnet";
}

[Cmdlet(VerbsCommon.Remove, "AzVirtualNetworkSubnet", SupportsShouldProcess = true)]
[OutputType(typeof(void))]
public class RemoveVirtualNetworkSubnetCmdlet : NetworkWriteCmdletBase
{
    [Parameter(Mandatory = true)]
    [Alias("ResourceName")]
    public string Name { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string VirtualNetworkName { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override string Command => "Remove-AzVirtualNetworkSubnet";

    protected override string Target => $"Subnet '{Name}' in virtual network '{VirtualNetworkName}'";

    protected override string Action => "Remove";
}
