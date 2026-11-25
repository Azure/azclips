// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.Remove, "AzNetworkInterface", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(bool))]
public class RemoveNetworkInterfaceCmdlet : NetworkRemoveCmdletBase
{
    protected override string ErrorId => "NetworkInterfaceError";

    protected override string CmdletName => "Remove-AzNetworkInterface";

    protected override string ResourceDisplayName => "network interface";
}
