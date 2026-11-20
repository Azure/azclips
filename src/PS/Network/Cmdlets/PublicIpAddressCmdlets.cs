// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzPublicIpAddress", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.PublicIPAddress")]
public class NewPublicIpAddressCmdlet : NetworkWriteCmdletBase
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

    [Parameter]
    [ValidateSet("Basic", "Standard", "StandardV2")]
    public string? Sku { get; set; }

    [Parameter]
    [ValidateSet("Regional", "Global")]
    public string? Tier { get; set; }

    [Parameter]
    [ValidateSet("Static", "Dynamic")]
    public string? AllocationMethod { get; set; }

    [Parameter]
    [Alias("IpAddressVersion")]
    [ValidateSet("IPv4", "IPv6")]
    public string? Version { get; set; }

    [Parameter]
    [ValidateSet("1", "2", "3")]
    public string[]? Zone { get; set; }

    [Parameter]
    public string? DomainNameLabel { get; set; }

    [Parameter]
    [ValidateSet("TenantReuse", "SubscriptionReuse", "ResourceGroupReuse", "NoReuse")]
    public string? DomainNameLabelScope { get; set; }

    [Parameter]
    public string? ReverseFqdn { get; set; }

    [Parameter]
    public int IdleTimeoutInMinutes { get; set; }

    [Parameter]
    public string? IpAddress { get; set; }

    [Parameter]
    [ValidateSet("VirtualNetworkInherited", "Enabled", "Disabled")]
    public string? DdosProtectionMode { get; set; }

    [Parameter]
    public string? EdgeZone { get; set; }

    protected override string Command => "New-AzPublicIpAddress";

    protected override string Target => $"Public IP address '{Name}' in resource group '{ResourceGroupName}'";
}

[Cmdlet(VerbsCommon.Get, "AzPublicIpAddress", DefaultParameterSetName = ListSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.PublicIPAddress")]
public class GetPublicIpAddressCmdlet : NetworkGetCmdletBase
{
    [Parameter(ParameterSetName = GetSet, Mandatory = true)]
    [Parameter(ParameterSetName = ListSet)]
    public string? ResourceGroupName { get; set; }

    [Parameter(ParameterSetName = GetSet, Mandatory = true)]
    [Alias("ResourceName")]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = GetSet)]
    public string? ExpandResource { get; set; }

    protected override string Noun => "AzPublicIpAddress";
}

[Cmdlet(VerbsCommon.Remove, "AzPublicIpAddress", SupportsShouldProcess = true)]
[OutputType(typeof(bool))]
public class RemovePublicIpAddressCmdlet : NetworkRemoveCmdletBase
{
    protected override string Command => "Remove-AzPublicIpAddress";

    protected override string ResourceType => "public IP address";
}
