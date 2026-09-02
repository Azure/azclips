// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Models;

public sealed record StorageBlob(
    string Name,
    string? BlobType,
    long? ContentLength,
    string? ContentType,
    string? ETag,
    string? LastModified,
    string? AccessTier,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyDictionary<string, string> Tags)
{
    public string? ContinuationToken { get; init; }
}
