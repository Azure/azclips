// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzPublicIpAddress", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.PublicIPAddress")]
public class NewPublicIpAddressCmdlet : NetworkCmdletBase
{
    [Alias("ResourceName")]
    [Parameter(Mandatory = true, HelpMessage = "The name of the public IP address.")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, HelpMessage = "The location of the resource.")]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true, HelpMessage = "The resource group name.")]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = false, HelpMessage = "The public IP SKU name. Default: Standard.")]
    [ValidateSet("Basic", "Standard", "StandardV2", IgnoreCase = true)]
    public string? Sku { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The public IP SKU tier: Regional or Global.")]
    public string? Tier { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The IP address allocation method: Static or Dynamic. Default: Dynamic for the Basic SKU, else Static.")]
    public string? AllocationMethod { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The IP address version: IPv4 or IPv6. Default: IPv4.")]
    public string? IpAddressVersion { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The relative DNS name label.")]
    public string? DomainNameLabel { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The scope of the DNS name label: TenantReuse, SubscriptionReuse, ResourceGroupReuse, or NoReuse.")]
    public string? DomainNameLabelScope { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The reverse FQDN.")]
    public string? ReverseFqdn { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The idle timeout in minutes, from 4 to 30. Default: 4.")]
    [ValidateRange(4, 30)]
    public int IdleTimeoutInMinutes { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The IP address to use.")]
    public string? IpAddress { get; set; }

    [Alias("IpTags")]
    [Parameter(Mandatory = false, HelpMessage = "IP tags as a hash table of tag type and tag value.")]
    public Hashtable? IpTag { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Name or ID of a public IP prefix.")]
    public string? PublicIpPrefix { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The DDoS protection mode: VirtualNetworkInherited, Enabled, or Disabled.")]
    public string? DdosProtectionMode { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Name or ID of the DDoS protection plan.")]
    public string? DdosProtectionPlanId { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Name or ID of the DDoS custom policy.")]
    public string? DdosCustomPolicyId { get; set; }

    [Alias("Zones")]
    [Parameter(Mandatory = false, HelpMessage = "Availability zones.")]
    public string[]? Zone { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The name of the edge zone.")]
    public string? EdgeZone { get; set; }

    [Alias("Tags")]
    [Parameter(Mandatory = false, HelpMessage = "Key-value pairs in the form of a hash table.")]
    public Hashtable? Tag { get; set; }

    protected override string ErrorId => "PublicIpAddressError";

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"public IP address '{Name}' in resource group '{ResourceGroupName}'", "Create"))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { "Name", Name },
            { "Location", Location },
            { "ResourceGroupName", ResourceGroupName },
        };
        if (IsBound(nameof(Sku)) && Sku is not null)                                   { parameters.Add("Sku", Sku); }
        if (IsBound(nameof(Tier)) && Tier is not null)                                 { parameters.Add("Tier", Tier); }
        if (IsBound(nameof(AllocationMethod)) && AllocationMethod is not null)         { parameters.Add("AllocationMethod", AllocationMethod); }
        if (IsBound(nameof(IpAddressVersion)) && IpAddressVersion is not null)         { parameters.Add("IpAddressVersion", IpAddressVersion); }
        if (IsBound(nameof(DomainNameLabel)) && DomainNameLabel is not null)           { parameters.Add("DomainNameLabel", DomainNameLabel); }
        if (IsBound(nameof(DomainNameLabelScope)) && DomainNameLabelScope is not null) { parameters.Add("DomainNameLabelScope", DomainNameLabelScope); }
        if (IsBound(nameof(ReverseFqdn)) && ReverseFqdn is not null)                   { parameters.Add("ReverseFqdn", ReverseFqdn); }
        if (IsBound(nameof(IdleTimeoutInMinutes)))                                     { parameters.Add("IdleTimeoutInMinutes", IdleTimeoutInMinutes); }
        if (IsBound(nameof(IpAddress)) && IpAddress is not null)                       { parameters.Add("IpAddress", IpAddress); }
        if (IsBound(nameof(IpTag)) && IpTag is not null)                               { parameters.Add("IpTag", NetworkCmdletArgumentConverter.ToPairs(IpTag)); }
        if (IsBound(nameof(PublicIpPrefix)) && PublicIpPrefix is not null)             { parameters.Add("PublicIpPrefix", PublicIpPrefix); }
        if (IsBound(nameof(DdosProtectionMode)) && DdosProtectionMode is not null)     { parameters.Add("DdosProtectionMode", DdosProtectionMode); }
        if (IsBound(nameof(DdosProtectionPlanId)) && DdosProtectionPlanId is not null) { parameters.Add("DdosProtectionPlanId", DdosProtectionPlanId); }
        if (IsBound(nameof(DdosCustomPolicyId)) && DdosCustomPolicyId is not null)     { parameters.Add("DdosCustomPolicyId", DdosCustomPolicyId); }
        if (IsBound(nameof(Zone)) && Zone is not null)                                 { parameters.Add("Zone", Zone); }
        if (IsBound(nameof(EdgeZone)) && EdgeZone is not null)                         { parameters.Add("EdgeZone", EdgeZone); }
        if (IsBound(nameof(Tag)) && Tag is not null)                                   { parameters.Add("Tag", NetworkCmdletArgumentConverter.ToStringDictionary(Tag)); }

        RunCommand("New-AzPublicIpAddress", parameters);
    }
}
