// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.Remove, "AzVirtualNetwork", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(bool))]
public class RemoveVirtualNetworkCmdlet : NetworkRemoveCmdletBase
{
    protected override string ErrorId => "VirtualNetworkError";

    protected override string CmdletName => "Remove-AzVirtualNetwork";

    protected override string ResourceDisplayName => "virtual network";
}
