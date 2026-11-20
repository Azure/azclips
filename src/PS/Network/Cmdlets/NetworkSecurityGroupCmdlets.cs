// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzNetworkSecurityGroup", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkSecurityGroup")]
public class NewNetworkSecurityGroupCmdlet : NetworkWriteCmdletBase
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

    protected override string Command => "New-AzNetworkSecurityGroup";

    protected override string Target => $"Network security group '{Name}' in resource group '{ResourceGroupName}'";
}

[Cmdlet(VerbsCommon.Get, "AzNetworkSecurityGroup", DefaultParameterSetName = ListSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkSecurityGroup")]
public class GetNetworkSecurityGroupCmdlet : NetworkGetCmdletBase
{
    [Parameter(ParameterSetName = GetSet, Mandatory = true)]
    [Parameter(ParameterSetName = ListSet)]
    public string? ResourceGroupName { get; set; }

    [Parameter(ParameterSetName = GetSet, Mandatory = true)]
    [Alias("ResourceName")]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = GetSet)]
    public string? ExpandResource { get; set; }

    protected override string Noun => "AzNetworkSecurityGroup";
}

[Cmdlet(VerbsCommon.Remove, "AzNetworkSecurityGroup", SupportsShouldProcess = true)]
[OutputType(typeof(bool))]
public class RemoveNetworkSecurityGroupCmdlet : NetworkRemoveCmdletBase
{
    protected override string Command => "Remove-AzNetworkSecurityGroup";

    protected override string ResourceType => "network security group";
}
