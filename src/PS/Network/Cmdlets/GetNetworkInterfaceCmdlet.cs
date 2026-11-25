// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.Get, "AzNetworkInterface", DefaultParameterSetName = ListParameterSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkInterface")]
public class GetNetworkInterfaceCmdlet : NetworkGetCmdletBase
{
    protected override string ErrorId => "NetworkInterfaceError";

    protected override string CmdletName => "Get-AzNetworkInterface";
}
