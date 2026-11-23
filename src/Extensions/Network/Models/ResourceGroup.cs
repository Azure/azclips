// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Network.Models;
/// <summary>Resource group information. External API model (Microsoft.Resources 2025-04-01), trimmed to the fields Network uses.</summary>
public partial class ResourceGroup
{
    /// <summary>Fully qualified resource ID for the resource group.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>The name of the resource group.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The geo-location where the resource group lives.</summary>
    [JsonPropertyName("location")]
    public string? Location { get; set; }
}
