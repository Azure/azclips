// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     Entity representing the reference to the template.
    public class TemplateLink
    {
        // The URI of the template to deploy. Use either the uri or id property, but not both.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Uri { get; set; }

        // The resource id of a Template Spec. Use either the id or uri property, but not both.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; }

        // The query string (for example, a SAS token) to be used with the templateLink URI.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? QueryString { get; set; }

        // The relativePath property can be used to deploy a linked template at a location relative to the parent.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RelativePath { get; set; }

        // If included, must match the ContentVersion in the template.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ContentVersion { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
