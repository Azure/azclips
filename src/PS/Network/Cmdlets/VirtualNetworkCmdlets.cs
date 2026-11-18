// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzVirtualNetwork", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.VirtualNetwork")]
public class NewVirtualNetworkCmdlet : NetworkWriteCmdletBase
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
    [Alias("AddressPrefix")]
    public string[]? AddressPrefixes { get; set; }

    [Parameter]
    [Alias("DnsServer")]
    public string[]? DnsServers { get; set; }

    [Parameter]
    public string? SubnetName { get; set; }

    [Parameter]
    public string[]? SubnetPrefixes { get; set; }

    [Parameter]
    public SwitchParameter EnableDdosProtection { get; set; }

    [Parameter]
    [Alias("DdosProtectionPlanId")]
    public string? DdosProtectionPlan { get; set; }

    [Parameter]
    [Alias("FlowTimeout")]
    public int FlowTimeoutInMinutes { get; set; }

    [Parameter]
    public string? BgpCommunity { get; set; }

    [Parameter]
    public SwitchParameter EnableEncryption { get; set; }

    [Parameter]
    [Alias("EncryptionEnforcementPolicy")]
    [ValidateSet("AllowUnencrypted", "DropUnencrypted")]
    public string? EncryptionEnforcement { get; set; }

    [Parameter]
    public string? EdgeZone { get; set; }

    protected override string Command => "New-AzVirtualNetwork";

    protected override string Target => $"Virtual network '{Name}' in resource group '{ResourceGroupName}'";
}

[Cmdlet(VerbsCommon.Get, "AzVirtualNetwork", DefaultParameterSetName = ListSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.VirtualNetwork")]
public class GetVirtualNetworkCmdlet : NetworkGetCmdletBase
{
    [Parameter(ParameterSetName = GetSet, Mandatory = true)]
    [Parameter(ParameterSetName = ListSet)]
    public string? ResourceGroupName { get; set; }

    [Parameter(ParameterSetName = GetSet, Mandatory = true)]
    [Alias("ResourceName")]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = GetSet)]
    public string? ExpandResource { get; set; }

    protected override string Noun => "AzVirtualNetwork";
}

[Cmdlet(VerbsCommon.Remove, "AzVirtualNetwork", SupportsShouldProcess = true)]
[OutputType(typeof(bool))]
public class RemoveVirtualNetworkCmdlet : NetworkRemoveCmdletBase
{
    protected override string Command => "Remove-AzVirtualNetwork";

    protected override string ResourceType => "virtual network";
}
