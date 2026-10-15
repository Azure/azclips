// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     The deployment export result. Returned by the deployment exportTemplate API.
    public class DeploymentExportResult
    {
        // The template content. The specification declares this property without a type: ARM
        // returns the deployment's ARM JSON template verbatim, so it is surfaced as a raw JSON
        // element rather than forced into a fixed schema.
        public JsonElement? Template { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
