using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Cirrus.Extensions.Rest.Models
{
    /// <summary>
    /// Untyped response for the universal <c>rest</c> passthrough command. Mirrors the surface of
    /// <c>az rest</c> / <c>Invoke-AzRestMethod</c>: the caller gets the raw HTTP status code, the
    /// response headers, and the response body. The body is deliberately NOT deserialized into any
    /// fixed model — it is surfaced as the raw string (<see cref="Content"/>) and, when the body is
    /// valid JSON, as a parsed <see cref="JsonElement"/> (<see cref="Json"/>).
    /// </summary>
    public class RestResponse
    {
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new();

        /// <summary>
        /// The raw response body as returned by the service. May be empty.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// The response body parsed as JSON when it is valid JSON; otherwise <c>null</c>. This lets
        /// callers navigate the payload without the command imposing a typed schema.
        /// </summary>
        [JsonPropertyName("json")]
        public JsonElement? Json { get; set; }
    }
}
