// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class StorageBlobCliOutput
{
    internal static JsonElement ProjectCopyStart(StorageBlobCopyStartResult result) =>
        JsonSerializer.SerializeToElement(new
        {
            copyId = result.CopyId,
            copyStatus = result.CopyStatus,
            etag = result.ETag,
            lastModified = result.LastModified.UtcDateTime
        });

    internal static JsonElement ProjectEntry(StorageBlobEntry entry, bool includeTags)
    {
        if (entry is StorageBlobPrefix prefix)
            return JsonSerializer.SerializeToElement(new { name = prefix.Name });
        if (entry is not StorageBlob blob)
            throw new InvalidDataException("Storage returned an unsupported listing entry.");
        var fields = Project(blob, includeTags).EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value, StringComparer.Ordinal);
        fields.Add("isCurrentVersion", blob.IsCurrentVersion);
        fields.Add("hasVersionsOnly", blob.HasVersionsOnly);
        fields.Add("objectReplicationMetadata", blob.ObjectReplicationMetadata);
        return JsonSerializer.SerializeToElement(fields);
    }

    internal static JsonElement Project(StorageBlob blob, bool includeTags)
    {
        var properties = blob.Properties;
        return JsonSerializer.SerializeToElement(new
        {
            content = "",
            deleted = blob.Deleted,
            metadata = blob.Metadata,
            name = blob.Name,
            properties = new
            {
                appendBlobCommittedBlockCount = blob.AppendBlobCommittedBlockCount,
                blobTier = properties.AccessTier,
                blobTierChangeTime = Date(properties.AccessTierChangeTime),
                blobTierInferred = properties.AccessTierInferred,
                blobType = properties.BlobType,
                contentLength = properties.ContentLength,
                contentRange = blob.ContentRange,
                contentSettings = new
                {
                    cacheControl = properties.CacheControl,
                    contentDisposition = properties.ContentDisposition,
                    contentEncoding = properties.ContentEncoding,
                    contentLanguage = properties.ContentLanguage,
                    contentMd5 = properties.ContentMd5,
                    contentType = properties.ContentType
                },
                copy = properties.CopyId is null && properties.CopyStatus is null && properties.CopySource is null ? null : new
                {
                    id = properties.CopyId,
                    completionTime = Date(properties.CopyCompletionTime),
                    status = properties.CopyStatus,
                    source = properties.CopySource,
                    progress = properties.CopyProgress,
                    statusDescription = properties.CopyStatusDescription
                },
                creationTime = Date(properties.CreationTime),
                deletedTime = Date(properties.DeletedTime),
                etag = properties.ETag,
                lastModified = Date(properties.LastModified),
                lease = new { duration = properties.LeaseDuration, state = properties.LeaseState, status = properties.LeaseStatus },
                pageBlobSequenceNumber = properties.BlobSequenceNumber,
                pageRanges = (object?)null,
                rehydrationStatus = properties.ArchiveStatus,
                remainingRetentionDays = properties.RemainingRetentionDays,
                serverEncrypted = properties.ServerEncrypted
            },
            snapshot = blob.Snapshot,
            versionId = blob.VersionId,
            tags = includeTags ? blob.Tags : null
        });
    }

    private static DateTimeOffset? Date(string? value)
    {
        if (value is null) return null;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            throw new InvalidDataException("Storage returned an invalid blob property timestamp.");
        return parsed.ToUniversalTime();
    }
}
