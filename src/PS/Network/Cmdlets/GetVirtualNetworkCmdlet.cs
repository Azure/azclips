// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.Get, "AzVirtualNetwork", DefaultParameterSetName = ListParameterSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.VirtualNetwork")]
public class GetVirtualNetworkCmdlet : NetworkGetCmdletBase
{
    protected override string ErrorId => "VirtualNetworkError";

    protected override string CmdletName => "Get-AzVirtualNetwork";
}
