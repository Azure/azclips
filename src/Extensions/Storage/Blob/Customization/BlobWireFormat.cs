// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>Wire formats the spec leaves to the client, shared by the blob write customizations.</summary>
    internal static class BlobWireFormat
    {
        internal const string MetadataPrefix = "x-ms-meta-";

        /// <summary>Parses <c>key=value</c> pairs; an empty key is an error and the last duplicate wins.</summary>
        internal static Dictionary<string, string> ParsePairs(string[]? pairs, string parameterName)
        {
            Dictionary<string, string> result = new(StringComparer.Ordinal);
            foreach (string pair in pairs ?? [])
            {
                int separator = pair.IndexOf('=');
                if (separator <= 0)
                {
                    throw new ArgumentException($"'{pair}' is not a key=value pair.", parameterName);
                }
                result[pair[..separator]] = pair[(separator + 1)..];
            }
            return result;
        }

        /// <summary><c>x-ms-tags</c> is a URL-encoded query string; null when there are no tags.</summary>
        internal static string? FormatTags(IReadOnlyDictionary<string, string> tags) =>
            tags.Count > 0
                ? string.Join("&", tags.Select(t => $"{Uri.EscapeDataString(t.Key)}={Uri.EscapeDataString(t.Value)}"))
                : null;

        /// <summary>Metadata is one <c>x-ms-meta-{key}</c> header per pair, not the single <c>x-ms-meta</c> the spec declares.</summary>
        internal static void AddMetadata(HttpRequestMessage request, IReadOnlyDictionary<string, string> metadata)
        {
            foreach (KeyValuePair<string, string> pair in metadata)
            {
                request.Headers.TryAddWithoutValidation(MetadataPrefix + pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// Escapes what a request URI cannot carry (spaces, non-ASCII) while keeping existing escapes and the
        /// query string as given, so an encoded URL or a SAS signature passes through unchanged.
        /// </summary>
        internal static string EncodeUrl(string url, string parameterName) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                ? uri.AbsoluteUri
                : throw new ArgumentException($"'{url}' is not an absolute URL.", parameterName);

        /// <summary>Response and content headers merged; <c>Last-Modified</c> is a content header in HttpClient even with no body.</summary>
        internal static Func<string, string?> ReadHeaders(HttpResponseMessage response)
        {
            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers.Concat(response.Content.Headers))
            {
                headers[header.Key] = string.Join(",", header.Value);
            }
            return name => headers.TryGetValue(name, out string? value) && value.Length > 0 ? value : null;
        }
    }
}
