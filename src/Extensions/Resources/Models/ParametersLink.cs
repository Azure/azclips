// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     Entity representing the reference to the deployment parameters.
    public class ParametersLink
    {
        // The URI of the parameters file.
        public string Uri { get; set; } = string.Empty;

        // If included, must match the ContentVersion in the template.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ContentVersion { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
