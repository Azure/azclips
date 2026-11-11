// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>Wire formats the spec leaves to the client, shared by the file customizations.</summary>
    internal static class FileWireFormat
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

        /// <summary>Metadata is one <c>x-ms-meta-{key}</c> header per pair, not the single <c>x-ms-meta</c> the spec declares.</summary>
        internal static void AddMetadata(HttpRequestMessage request, IReadOnlyDictionary<string, string> metadata)
        {
            foreach (KeyValuePair<string, string> pair in metadata)
            {
                request.Headers.TryAddWithoutValidation(MetadataPrefix + pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// <c>x-ms-copy-source</c> must be URL-encoded. <see cref="Uri.AbsoluteUri"/> escapes what needs escaping and leaves
        /// existing escapes (and a SAS query string) as given.
        /// </summary>
        internal static string EncodeUrl(string url, string parameterName) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                ? uri.AbsoluteUri
                : throw new ArgumentException($"'{url}' is not an absolute URL.", parameterName);

        /// <summary>Response and content headers merged; <c>Last-Modified</c> is a content header in HttpClient even with no body.</summary>
        internal static IReadOnlyDictionary<string, string> ReadHeaders(HttpResponseMessage response)
        {
            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers.Concat(response.Content.Headers))
            {
                headers[header.Key] = string.Join(",", header.Value);
            }
            return headers;
        }

        /// <summary>The <c>x-ms-meta-{key}</c> response headers as a dictionary, or null when there are none.</summary>
        internal static IDictionary<string, string>? ReadMetadata(IReadOnlyDictionary<string, string> headers)
        {
            Dictionary<string, string> metadata = headers
                .Where(h => h.Key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key[MetadataPrefix.Length..], h => h.Value);
            return metadata.Count > 0 ? metadata : null;
        }
    }
}
