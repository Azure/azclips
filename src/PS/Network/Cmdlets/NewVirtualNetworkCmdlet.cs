// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzVirtualNetwork", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.VirtualNetwork")]
public class NewVirtualNetworkCmdlet : NetworkCmdletBase
{
    [Alias("ResourceName")]
    [Parameter(Mandatory = true, HelpMessage = "The name of the virtual network.")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, HelpMessage = "The location of the resource.")]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true, HelpMessage = "The resource group name.")]
    public string ResourceGroupName { get; set; } = "";

    [Alias("AddressPrefixes")]
    [Parameter(Mandatory = false, HelpMessage = "Address prefixes of the virtual network. Default: 10.0.0.0/16, unless -IpamPoolPrefixAllocation is given.")]
    public string[]? AddressPrefix { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Names of subnets to create with the virtual network.")]
    public string[]? SubnetNames { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Address prefixes of each subnet; item i belongs to subnet name i.")]
    public string[][]? SubnetAddressPrefixes { get; set; }

    [Alias("DnsServers")]
    [Parameter(Mandatory = false, HelpMessage = "DNS server IP addresses.")]
    public string[]? DnsServer { get; set; }

    [Alias("Tags")]
    [Parameter(Mandatory = false, HelpMessage = "Key-value pairs in the form of a hash table.")]
    public Hashtable? Tag { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable DDoS protection for the virtual network.")]
    public SwitchParameter EnableDdosProtection { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Resource ID of the DDoS protection plan.")]
    public string? DdosProtectionPlanId { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable VM protection for all subnets in the virtual network.")]
    public SwitchParameter EnableVmProtection { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The FlowTimeout value (in minutes), from 4 to 30.")]
    [ValidateRange(4, 30)]
    public int FlowTimeout { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The BGP community associated with the virtual network.")]
    public string? BgpCommunity { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Enable encryption on the virtual network.")]
    public SwitchParameter EnableEncryption { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Whether the encrypted virtual network allows VMs that do not support encryption: DropUnencrypted or AllowUnencrypted.")]
    public string? EncryptionEnforcementPolicy { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The name of the edge zone.")]
    public string? EdgeZone { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Private endpoint virtual network policies: Disabled or Basic.")]
    public string? PrivateEndpointVNetPoliciesValue { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "IP allocation resource IDs.")]
    public string[]? IpAllocation { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "IPAM pool resource IDs for the address space.")]
    public string[]? IpamPoolPrefixAllocation { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Summarized gateway address prefixes.")]
    public string[]? SummarizedGatewayPrefix { get; set; }

    protected override string ErrorId => "VirtualNetworkError";

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"virtual network '{Name}' in resource group '{ResourceGroupName}'", "Create"))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { "Name", Name },
            { "Location", Location },
            { "ResourceGroupName", ResourceGroupName },
        };
        if (IsBound(nameof(AddressPrefix)) && AddressPrefix is not null)                       { parameters.Add("AddressPrefix", AddressPrefix); }
        if (IsBound(nameof(SubnetNames)) && SubnetNames is not null)                           { parameters.Add("SubnetNames", SubnetNames.ToList()); }
        if (IsBound(nameof(SubnetAddressPrefixes)) && SubnetAddressPrefixes is not null)       { parameters.Add("SubnetAddressPrefixes", NetworkCmdletArgumentConverter.ToNestedList(SubnetAddressPrefixes)); }
        if (IsBound(nameof(DnsServer)) && DnsServer is not null)                               { parameters.Add("DnsServer", DnsServer); }
        if (IsBound(nameof(Tag)) && Tag is not null)                                           { parameters.Add("Tag", NetworkCmdletArgumentConverter.ToStringDictionary(Tag)); }
        if (IsBound(nameof(EnableDdosProtection)))                                             { parameters.Add("EnableDdosProtection", EnableDdosProtection.IsPresent); }
        if (IsBound(nameof(DdosProtectionPlanId)) && DdosProtectionPlanId is not null)         { parameters.Add("DdosProtectionPlanId", DdosProtectionPlanId); }
        if (IsBound(nameof(EnableVmProtection)))                                               { parameters.Add("EnableVmProtection", EnableVmProtection.IsPresent); }
        if (IsBound(nameof(FlowTimeout)))                                                      { parameters.Add("FlowTimeout", FlowTimeout); }
        if (IsBound(nameof(BgpCommunity)) && BgpCommunity is not null)                         { parameters.Add("BgpCommunity", BgpCommunity); }
        if (IsBound(nameof(EnableEncryption)))                                                 { parameters.Add("EnableEncryption", EnableEncryption.IsPresent); }
        if (IsBound(nameof(EncryptionEnforcementPolicy)) && EncryptionEnforcementPolicy is not null) { parameters.Add("EncryptionEnforcementPolicy", EncryptionEnforcementPolicy); }
        if (IsBound(nameof(EdgeZone)) && EdgeZone is not null)                                 { parameters.Add("EdgeZone", EdgeZone); }
        if (IsBound(nameof(PrivateEndpointVNetPoliciesValue)) && PrivateEndpointVNetPoliciesValue is not null) { parameters.Add("PrivateEndpointVNetPoliciesValue", PrivateEndpointVNetPoliciesValue); }
        if (IsBound(nameof(IpAllocation)) && IpAllocation is not null)                         { parameters.Add("IpAllocation", IpAllocation); }
        if (IsBound(nameof(IpamPoolPrefixAllocation)) && IpamPoolPrefixAllocation is not null) { parameters.Add("IpamPoolPrefixAllocation", IpamPoolPrefixAllocation); }
        if (IsBound(nameof(SummarizedGatewayPrefix)) && SummarizedGatewayPrefix is not null)   { parameters.Add("SummarizedGatewayPrefix", SummarizedGatewayPrefix); }

        RunCommand("New-AzVirtualNetwork", parameters);
    }
}
