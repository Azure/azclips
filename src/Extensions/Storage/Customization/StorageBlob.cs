// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed record StorageBlobContainer(string Name, StorageContext Context)
{
    public Uri Uri => Context.GetBlobUri(Name);
}

public abstract record StorageBlobEntry(string Name);

public sealed record StorageBlobPrefix(string Name, StorageBlobContainer Container) : StorageBlobEntry(Name)
{
    public Uri Uri => Container.Context.GetBlobUri(Container.Name, Name);
}

public sealed record StorageBlob(string Name, StorageBlobContainer Container, BlobModels.BlobProperties Properties) : StorageBlobEntry(Name)
{
    public StorageContext Context => Container.Context;
    public string? Snapshot { get; init; }
    public string? VersionId { get; init; }
    public bool? IsCurrentVersion { get; init; }
    public bool Deleted { get; init; }
    public bool? HasVersionsOnly { get; init; }
    public int? AppendBlobCommittedBlockCount { get; init; }
    public string? ContentRange { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = EmptyMap;
    public IReadOnlyDictionary<string, string> Tags { get; init; } = EmptyMap;
    public IReadOnlyDictionary<string, string> ObjectReplicationMetadata { get; init; } = EmptyMap;
    public StorageBlobContinuationToken? ContinuationToken { get; init; }
    public Uri Uri => new(Container.Context.GetBlobUri(Container.Name, Name).AbsoluteUri
        .WithQuery("snapshot", Snapshot).WithQuery("versionid", VersionId));

    internal static readonly IReadOnlyDictionary<string, string> EmptyMap =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    internal static StorageBlob FromHeaders(StorageBlobContainer container, string name, StorageResourceProperties headers,
        string? snapshot, string? versionId)
    {
        string? Header(string key) => headers.Headers.TryGetValue(key, out string? value) ? value : null;
        long? Number(string key)
        {
            if (Header(key) is not { } value) return null;
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                throw new InvalidDataException("Storage returned an invalid blob numeric property header.");
            return number;
        }
        bool? Boolean(string key)
        {
            if (Header(key) is not { } value) return null;
            if (!bool.TryParse(value, out bool boolean))
                throw new InvalidDataException("Storage returned an invalid blob boolean property header.");
            return boolean;
        }
        long? committedBlocks = Number("x-ms-blob-committed-block-count");
        if (committedBlocks > int.MaxValue)
            throw new InvalidDataException("Storage returned an invalid committed-block count.");
        byte[]? contentMd5;
        try
        {
            contentMd5 = Header("Content-MD5") is { } md5 ? Convert.FromBase64String(md5) : null;
        }
        catch (FormatException)
        {
            throw new InvalidDataException("Storage returned an invalid Content-MD5 header.");
        }
        return new StorageBlob(name, container, new BlobModels.BlobProperties
        {
            ETag = headers.ETag ?? throw new InvalidDataException("Storage did not return the requested blob's ETag."),
            LastModified = headers.LastModified?.ToString("R", CultureInfo.InvariantCulture)
                ?? throw new InvalidDataException("Storage did not return the requested blob's last-modified time."),
            ContentLength = headers.ContentLength,
            ContentType = headers.ContentType,
            ContentEncoding = Header("Content-Encoding"),
            ContentLanguage = Header("Content-Language"),
            CacheControl = Header("Cache-Control"),
            ContentDisposition = Header("Content-Disposition"),
            ContentMd5 = contentMd5,
            BlobType = Header("x-ms-blob-type"),
            AccessTier = Header("x-ms-access-tier"),
            CreationTime = Header("x-ms-creation-time"),
            AccessTierChangeTime = Header("x-ms-access-tier-change-time"),
            AccessTierInferred = Boolean("x-ms-access-tier-inferred"),
            ArchiveStatus = Header("x-ms-archive-status"),
            BlobSequenceNumber = Number("x-ms-blob-sequence-number"),
            ServerEncrypted = Boolean("x-ms-server-encrypted"),
            CopyId = Header("x-ms-copy-id"), CopyStatus = Header("x-ms-copy-status"),
            CopySource = StorageCopyAuthorization.PublicSource(Header("x-ms-copy-source")), CopyProgress = Header("x-ms-copy-progress"),
            CopyCompletionTime = Header("x-ms-copy-completion-time"),
            CopyStatusDescription = StorageCopyAuthorization.StatusDescription(Header("x-ms-copy-status-description")),
            LeaseStatus = Header("x-ms-lease-status"), LeaseState = Header("x-ms-lease-state"), LeaseDuration = Header("x-ms-lease-duration")
        })
        {
            // A base-blob HEAD may return a version ID without selecting that immutable version.
            Metadata = headers.Metadata, Snapshot = snapshot, VersionId = versionId,
            AppendBlobCommittedBlockCount = (int?)committedBlocks, ContentRange = Header("Content-Range")
        };
    }
}

public sealed record StorageBlobContinuationToken(string NextMarker, string StorageAccountName, string Container,
    string? Prefix, string? Delimiter);

public sealed record StorageBlobNextMarker([property: JsonPropertyName("nextMarker")] string? NextMarker);
public sealed record StorageBlobExistence([property: JsonPropertyName("exists")] bool Exists);
