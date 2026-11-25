// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.Get, "AzPublicIpAddress", DefaultParameterSetName = ListParameterSet)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.PublicIPAddress")]
public class GetPublicIpAddressCmdlet : NetworkGetCmdletBase
{
    protected override string ErrorId => "PublicIpAddressError";

    protected override string CmdletName => "Get-AzPublicIpAddress";
}
