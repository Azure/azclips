// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.Remove, "AzNetworkSecurityGroup", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(bool))]
public class RemoveNetworkSecurityGroupCmdlet : NetworkRemoveCmdletBase
{
    protected override string ErrorId => "NetworkSecurityGroupError";

    protected override string CmdletName => "Remove-AzNetworkSecurityGroup";

    protected override string ResourceDisplayName => "network security group";
}
