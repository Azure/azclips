// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// The inbound rule legacy Azure CLI gives a network security group that vm create creates:
/// RDP for a Windows image and SSH for anything else (azure-cli <c>custom.py</c>, and
/// <c>build_nsg_resource</c> in <c>_template_builder.py</c>).
/// </summary>
internal static class NetworkSecurityGroupDefaultRule
{
    internal static SecurityRule For(string osType)
    {
        bool windows = string.Equals(osType, "Windows", StringComparison.OrdinalIgnoreCase);
        return new SecurityRule
        {
            Name = windows ? "rdp" : "default-allow-ssh",
            Properties = new SecurityRuleProperties
            {
                Protocol = "Tcp",
                SourcePortRange = "*",
                DestinationPortRange = windows ? "3389" : "22",
                SourceAddressPrefix = "*",
                DestinationAddressPrefix = "*",
                Access = "Allow",
                Priority = 1000,
                Direction = "Inbound",
            },
        };
    }
}
