// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.Remove, "AzPublicIpAddress", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(bool))]
public class RemovePublicIpAddressCmdlet : NetworkRemoveCmdletBase
{
    protected override string ErrorId => "PublicIpAddressError";

    protected override string CmdletName => "Remove-AzPublicIpAddress";

    protected override string ResourceDisplayName => "public IP address";
}
