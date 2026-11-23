// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzNetworkInterface", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkInterface")]
public class NewNetworkInterfaceCmdlet : NetworkCmdletBase
{
    [Alias("ResourceName")]
    [Parameter(Mandatory = true, HelpMessage = "The name of the network interface.")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, HelpMessage = "The location of the resource.")]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true, HelpMessage = "The resource group name.")]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true, HelpMessage = "The resource ID of the subnet.")]
    public string SubnetId { get; set; } = "";

    [Parameter(Mandatory = false, HelpMessage = "The resource ID of the public IP address.")]
    public string? PublicIpAddressId { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The resource ID of the network security group.")]
    public string? NetworkSecurityGroupId { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The static private IP address. Without it the allocation is dynamic.")]
    public string? PrivateIpAddress { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The name of the IP configuration. Default: ipconfig1.")]
    public string? IpConfigurationName { get; set; }

    [Alias("DnsServers")]
    [Parameter(Mandatory = false, HelpMessage = "DNS server IP addresses.")]
    public string[]? DnsServer { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The internal DNS name label.")]
    public string? InternalDnsNameLabel { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable accelerated networking.")]
    public SwitchParameter EnableAcceleratedNetworking { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable IP forwarding.")]
    public SwitchParameter EnableIPForwarding { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The name of the edge zone.")]
    public string? EdgeZone { get; set; }

    [Alias("Tags")]
    [Parameter(Mandatory = false, HelpMessage = "Key-value pairs in the form of a hash table.")]
    public Hashtable? Tag { get; set; }

    protected override string ErrorId => "NetworkInterfaceError";

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"network interface '{Name}' in resource group '{ResourceGroupName}'", "Create"))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { "Name", Name },
            { "Location", Location },
            { "ResourceGroupName", ResourceGroupName },
            { "SubnetId", SubnetId },
        };
        if (IsBound(nameof(PublicIpAddressId)) && PublicIpAddressId is not null)           { parameters.Add("PublicIpAddressId", PublicIpAddressId); }
        if (IsBound(nameof(NetworkSecurityGroupId)) && NetworkSecurityGroupId is not null) { parameters.Add("NetworkSecurityGroupId", NetworkSecurityGroupId); }
        if (IsBound(nameof(PrivateIpAddress)) && PrivateIpAddress is not null)             { parameters.Add("PrivateIpAddress", PrivateIpAddress); }
        if (IsBound(nameof(IpConfigurationName)) && IpConfigurationName is not null)       { parameters.Add("IpConfigurationName", IpConfigurationName); }
        if (IsBound(nameof(DnsServer)) && DnsServer is not null)                           { parameters.Add("DnsServer", DnsServer); }
        if (IsBound(nameof(InternalDnsNameLabel)) && InternalDnsNameLabel is not null)     { parameters.Add("InternalDnsNameLabel", InternalDnsNameLabel); }
        if (IsBound(nameof(EnableAcceleratedNetworking)))                                  { parameters.Add("EnableAcceleratedNetworking", EnableAcceleratedNetworking.IsPresent); }
        if (IsBound(nameof(EnableIPForwarding)))                                           { parameters.Add("EnableIPForwarding", EnableIPForwarding.IsPresent); }
        if (IsBound(nameof(EdgeZone)) && EdgeZone is not null)                             { parameters.Add("EdgeZone", EdgeZone); }
        if (IsBound(nameof(Tag)) && Tag is not null)                                       { parameters.Add("Tag", NetworkCmdletArgumentConverter.ToStringDictionary(Tag)); }

        RunCommand("New-AzNetworkInterface", parameters);
    }
}
