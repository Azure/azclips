// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed record StorageResourceProperties(
    HttpStatusCode StatusCode,
    string? ETag,
    DateTimeOffset? LastModified,
    long? ContentLength,
    string? ContentType,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyDictionary<string, string> Headers)
{
    internal static StorageResourceProperties FromResponse(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
            headers.Add(header.Key, string.Join(",", header.Value));
        foreach (var header in response.Content.Headers)
            headers[header.Key] = string.Join(",", header.Value);
        var metadata = headers.Where(header => header.Key.StartsWith("x-ms-meta-", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(header => header.Key["x-ms-meta-".Length..], header => header.Value, StringComparer.OrdinalIgnoreCase);
        return new StorageResourceProperties(response.StatusCode, response.Headers.ETag?.ToString(),
            response.Content.Headers.LastModified, response.Content.Headers.ContentLength,
            response.Content.Headers.ContentType?.ToString(),
            new ReadOnlyDictionary<string, string>(metadata),
            new ReadOnlyDictionary<string, string>(headers));
    }
}
