// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

/// <summary>Inputs shared by security rule create and update.</summary>
public abstract class SecurityRuleWriteCmdletBase : NetworkWriteCmdletBase
{
    [Parameter(Mandatory = true)]
    [Alias("ResourceName")]
    public string Name { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string NetworkSecurityGroupName { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = string.Empty;

    [Parameter]
    [ValidateSet("Allow", "Deny")]
    public string? Access { get; set; }

    [Parameter]
    [ValidateSet("Inbound", "Outbound")]
    public string? Direction { get; set; }

    [Parameter]
    [ValidateSet("Tcp", "Udp", "Icmp", "Esp", "Ah", "*")]
    public string? Protocol { get; set; }

    [Parameter]
    [Alias("SourceAddressPrefix")]
    public string[]? SourceAddressPrefixes { get; set; }

    [Parameter]
    [Alias("SourcePortRange")]
    public string[]? SourcePortRanges { get; set; }

    [Parameter]
    [Alias("DestinationAddressPrefix")]
    public string[]? DestinationAddressPrefixes { get; set; }

    [Parameter]
    [Alias("DestinationPortRange")]
    public string[]? DestinationPortRanges { get; set; }

    [Parameter]
    [Alias("SourceApplicationSecurityGroupId")]
    public string[]? SourceAsgs { get; set; }

    [Parameter]
    [Alias("DestinationApplicationSecurityGroupId")]
    public string[]? DestinationAsgs { get; set; }

    [Parameter]
    public string? Description { get; set; }

    protected override string Target => $"Security rule '{Name}' in network security group '{NetworkSecurityGroupName}'";
}

[Cmdlet(VerbsCommon.New, "AzNetworkSecurityRule", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.SecurityRule")]
public class NewNetworkSecurityRuleCmdlet : SecurityRuleWriteCmdletBase
{
    [Parameter(Mandatory = true)]
    public int Priority { get; set; }

    protected override string Command => "New-AzNetworkSecurityRule";
}

[Cmdlet(VerbsCommon.Set, "AzNetworkSecurityRule", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.SecurityRule")]
public class SetNetworkSecurityRuleCmdlet : SecurityRuleWriteCmdletBase
{
    [Parameter]
    public int Priority { get; set; }

    protected override string Command => "Set-AzNetworkSecurityRule";

    protected override string Action => "Update";
}

[Cmdlet(VerbsCommon.Get, "AzNetworkSecurityRule", DefaultParameterSetName = ListSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.SecurityRule")]
public class GetNetworkSecurityRuleCmdlet : NetworkGetCmdletBase
{
    [Parameter(ParameterSetName = GetSet, Mandatory = true)]
    [Alias("ResourceName")]
    public string? Name { get; set; }

    [Parameter(Mandatory = true)]
    public string NetworkSecurityGroupName { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override string Noun => "AzNetworkSecurityRule";
}

[Cmdlet(VerbsCommon.Remove, "AzNetworkSecurityRule", SupportsShouldProcess = true)]
[OutputType(typeof(void))]
public class RemoveNetworkSecurityRuleCmdlet : NetworkWriteCmdletBase
{
    [Parameter(Mandatory = true)]
    [Alias("ResourceName")]
    public string Name { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string NetworkSecurityGroupName { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override string Command => "Remove-AzNetworkSecurityRule";

    protected override string Target => $"Security rule '{Name}' in network security group '{NetworkSecurityGroupName}'";

    protected override string Action => "Remove";
}
