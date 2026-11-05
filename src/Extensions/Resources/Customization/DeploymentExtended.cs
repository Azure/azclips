// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models;

public partial class DeploymentExtended
{
    // Legacy Az.Resources PSResourceGroupDeployment properties, kept for script compatibility and pipeline
    // binding by property name. Hidden from JSON so CLI output keeps the ARM shape.
    [JsonIgnore, IgnoreDataMember]
    public string? DeploymentName => Name;

    [JsonIgnore, IgnoreDataMember]
    public string? ResourceGroupName
    {
        get
        {
            string[] segments = Id?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
            int index = Array.FindIndex(segments, s => s.Equals("resourceGroups", StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < segments.Length ? segments[index + 1] : null;
        }
    }
}
