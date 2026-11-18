// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzNetworkInterface", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkInterface")]
public class NewNetworkInterfaceCmdlet : NetworkWriteCmdletBase
{
    [Parameter(Mandatory = true)]
    [Alias("ResourceName")]
    public string Name { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = string.Empty;

    [Parameter]
    public string? Location { get; set; }

    [Parameter]
    [Alias("Tags")]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = true)]
    [Alias("SubnetId")]
    public string Subnet { get; set; } = string.Empty;

    [Parameter]
    public string? VirtualNetworkName { get; set; }

    [Parameter]
    public string? IpConfigurationName { get; set; }

    [Parameter]
    public string? PrivateIpAddress { get; set; }

    [Parameter]
    [ValidateSet("IPv4", "IPv6")]
    public string? PrivateIpAddressVersion { get; set; }

    [Parameter]
    [Alias("PublicIpAddressId")]
    public string? PublicIpAddress { get; set; }

    [Parameter]
    [Alias("NetworkSecurityGroupId")]
    public string? NetworkSecurityGroup { get; set; }

    [Parameter]
    [Alias("DnsServer")]
    public string[]? DnsServers { get; set; }

    [Parameter]
    public string? InternalDnsNameLabel { get; set; }

    [Parameter]
    public SwitchParameter EnableIPForwarding { get; set; }

    [Parameter]
    public SwitchParameter EnableAcceleratedNetworking { get; set; }

    [Parameter]
    public string? EdgeZone { get; set; }

    protected override string Command => "New-AzNetworkInterface";

    protected override string Target => $"Network interface '{Name}' in resource group '{ResourceGroupName}'";
}

[Cmdlet(VerbsCommon.Get, "AzNetworkInterface", DefaultParameterSetName = ListSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkInterface")]
public class GetNetworkInterfaceCmdlet : NetworkGetCmdletBase
{
    [Parameter(ParameterSetName = GetSet, Mandatory = true)]
    [Parameter(ParameterSetName = ListSet)]
    public string? ResourceGroupName { get; set; }

    [Parameter(ParameterSetName = GetSet, Mandatory = true)]
    [Alias("ResourceName")]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = GetSet)]
    public string? ExpandResource { get; set; }

    protected override string Noun => "AzNetworkInterface";
}

/// <summary>
/// Removes a network interface. The impact is High, so PowerShell asks before the delete
/// unless <c>-Force</c> is set. <c>-WhatIf</c> and <c>-Confirm</c> still win over <c>-Force</c>.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "AzNetworkInterface", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(bool))]
public class RemoveNetworkInterfaceCmdlet : NetworkRemoveCmdletBase
{
    protected override string Command => "Remove-AzNetworkInterface";

    protected override string ResourceType => "network interface";

    protected override bool ConfirmRemove(string target)
    {
        var bound = MyInvocation.BoundParameters;
        var ask = !Force.IsPresent || bound.ContainsKey("WhatIf") || bound.ContainsKey("Confirm");
        return !ask || ShouldProcess(target, "Remove");
    }
}
