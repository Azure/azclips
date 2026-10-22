// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    /// <summary>
    /// Untyped response envelope for <c>az rest --full</c> and <c>Invoke-AzRestMethod</c>:
    /// the caller gets the raw HTTP status code, the
    /// response headers, and the response body. The body is deliberately NOT deserialized into any
    /// fixed model — it is surfaced as a string (<see cref="Content"/>) and, when recognized
    /// as JSON by the client, as a parsed <see cref="JsonElement"/>
    /// (<see cref="Json"/>).
    /// </summary>
    public class RestResponse
    {
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The response body. PowerShell prettifies JSON objects; CLI preserves the raw content.
        /// May be empty.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// The response body parsed as JSON when parsing succeeds; otherwise <c>null</c>.
        /// PowerShell additionally requires a JSON media type (Content-Type contains "json");
        /// CLI attempts parsing regardless of content type. This lets callers navigate the payload
        /// without the command imposing a typed schema.
        /// </summary>
        [JsonPropertyName("json")]
        public JsonElement? Json { get; set; }
    }
}
