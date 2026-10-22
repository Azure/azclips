// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    /// <summary>
    /// Untyped response envelope for <c>az rest --full-response</c> and
    /// <c>Invoke-AzRestMethod -FullResponse</c>:
    /// the caller gets the raw HTTP status code, the
    /// response headers, and the response body. The body is deliberately NOT deserialized into any
    /// fixed model — it is surfaced as a string (<see cref="Content"/>) and, when recognized
    /// as JSON by the client, as a parsed <see cref="JsonElement"/>
    /// (<see cref="Json"/>).
    /// </summary>
    public class RestResponse : IOutputProjection
    {
        /// <summary>Selects the host output view without changing the serialized response envelope.</summary>
        [JsonIgnore]
        public bool FullResponse { get; set; }

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
        /// The response body parsed as JSON when parsing succeeds; otherwise <c>null</c>.
        /// Both clients attempt parsing regardless of content type. This lets callers navigate the payload
        /// without the command imposing a typed schema.
        /// </summary>
        [JsonPropertyName("json")]
        public JsonElement? Json { get; set; }

        public object? GetOutputValue()
        {
            if (FullResponse)
                return this;

            if (Content.Length == 0)
                return null;

            return Json.HasValue ? (object)Json.Value : Content;
        }
    }
}
