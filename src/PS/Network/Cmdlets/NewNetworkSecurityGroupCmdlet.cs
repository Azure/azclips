// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

[Cmdlet(VerbsCommon.New, "AzNetworkSecurityGroup", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkSecurityGroup")]
public class NewNetworkSecurityGroupCmdlet : NetworkCmdletBase
{
    [Alias("ResourceName")]
    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "The name of the network security group.")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "The resource group name.")]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, HelpMessage = "The location of the resource.")]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true, HelpMessage = "Key-value pairs in the form of a hash table.")]
    public Hashtable? Tag { get; set; }

    protected override string ErrorId => "NetworkSecurityGroupError";

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"network security group '{Name}' in resource group '{ResourceGroupName}'", "Create"))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { "Name", Name },
            { "ResourceGroupName", ResourceGroupName },
            { "Location", Location },
        };
        if (IsBound(nameof(Tag)) && Tag is not null) { parameters.Add("Tag", NetworkCmdletArgumentConverter.ToStringDictionary(Tag)); }

        RunCommand("New-AzNetworkSecurityGroup", parameters);
    }
}
