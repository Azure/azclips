// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.Get, "AzNetworkSecurityGroup", DefaultParameterSetName = ListParameterSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkSecurityGroup")]
public class GetNetworkSecurityGroupCmdlet : NetworkGetCmdletBase
{
    protected override string ErrorId => "NetworkSecurityGroupError";

    protected override string CmdletName => "Get-AzNetworkSecurityGroup";
}
