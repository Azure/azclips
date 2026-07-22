// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    /// <summary>
    /// Untyped response for the universal <c>rest</c> passthrough command. Mirrors the surface of
    /// <c>az rest</c> / <c>Invoke-AzRestMethod</c>: the caller gets the raw HTTP status code, the
    /// response headers, and the response body. The body is deliberately NOT deserialized into any
    /// fixed model — it is surfaced as the raw string (<see cref="Content"/>) and, when the response
    /// advertises a JSON media type and parses successfully, as a parsed <see cref="JsonElement"/>
    /// (<see cref="Json"/>).
    /// </summary>
    public class RestResponse
    {
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The raw response body as returned by the service. May be empty.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// The response body parsed as JSON when the response advertises a JSON media type (its
        /// Content-Type contains "json") and the body parses successfully; otherwise <c>null</c>.
        /// Note: a body that happens to be valid JSON but is served with a non-JSON Content-Type is
        /// surfaced via <see cref="Content"/> only, and this stays <c>null</c>. This lets callers
        /// navigate the payload without the command imposing a typed schema.
        /// </summary>
        [JsonPropertyName("json")]
        public JsonElement? Json { get; set; }
    }
}
