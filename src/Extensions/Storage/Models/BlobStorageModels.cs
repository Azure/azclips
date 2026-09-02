// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Models;

[XmlRoot("EnumerationResults")]
public sealed class ListBlobsResponse
{
    [XmlAttribute]
    public string? ServiceEndpoint { get; set; }

    [XmlAttribute]
    public string? ContainerName { get; set; }

    public string? Prefix { get; set; }
    public string? Marker { get; set; }
    public int? MaxResults { get; set; }

    [XmlArray("Blobs")]
    [XmlArrayItem("Blob")]
    public BlobItem[] Blobs { get; set; } = [];

    public string? NextMarker { get; set; }
}

[XmlType("Blob")]
public sealed class BlobItem
{
    public BlobName Name { get; set; } = new();
    public bool? Deleted { get; set; }
    public string? Snapshot { get; set; }
    public string? VersionId { get; set; }
    public bool? IsCurrentVersion { get; set; }
    public BlobProperties Properties { get; set; } = new();
    public BlobMetadata? Metadata { get; set; }

    [XmlElement("BlobTags")]
    public BlobTags? Tags { get; set; }

    [XmlElement("OrMetadata")]
    public ObjectReplicationMetadata? OrMetadata { get; set; }

    public bool? HasVersionsOnly { get; set; }
}

public sealed class BlobName
{
    [XmlAttribute]
    public bool Encoded { get; set; }

    [XmlIgnore]
    public bool EncodedSpecified { get; set; }

    [XmlText]
    public string Content { get; set; } = string.Empty;
}

[XmlType("Properties")]
public sealed class BlobProperties
{
    [XmlElement("Creation-Time")]
    public string? CreationTime { get; set; }

    [XmlElement("Last-Modified")]
    public string? LastModified { get; set; }

    [XmlElement("Etag")]
    public string? ETag { get; set; }

    [XmlElement("Content-Length")]
    public long? ContentLength { get; set; }

    [XmlElement("Content-Type")]
    public string? ContentType { get; set; }

    [XmlElement("Content-Encoding")]
    public string? ContentEncoding { get; set; }

    [XmlElement("Content-Language")]
    public string? ContentLanguage { get; set; }

    [XmlElement("Content-MD5")]
    public string? ContentMd5 { get; set; }

    [XmlElement("Content-Disposition")]
    public string? ContentDisposition { get; set; }

    [XmlElement("Cache-Control")]
    public string? CacheControl { get; set; }

    [XmlElement("x-ms-blob-sequence-number")]
    public long? BlobSequenceNumber { get; set; }

    public BlobType? BlobType { get; set; }
    public LeaseStatus? LeaseStatus { get; set; }
    public LeaseState? LeaseState { get; set; }
    public LeaseDuration? LeaseDuration { get; set; }
    public string? CopyId { get; set; }
    public CopyStatus? CopyStatus { get; set; }
    public string? CopySource { get; set; }
    public string? CopyProgress { get; set; }
    public string? CopyCompletionTime { get; set; }
    public string? CopyStatusDescription { get; set; }
    public bool? ServerEncrypted { get; set; }
    public bool? IncrementalCopy { get; set; }
    public string? DestinationSnapshot { get; set; }
    public string? DeletedTime { get; set; }
    public int? RemainingRetentionDays { get; set; }
    public string? AccessTier { get; set; }
    public bool? AccessTierInferred { get; set; }
    public string? ArchiveStatus { get; set; }
    public string? CustomerProvidedKeySha256 { get; set; }
    public string? EncryptionScope { get; set; }
    public string? AccessTierChangeTime { get; set; }
    public int? TagCount { get; set; }

    [XmlElement("Expiry-Time")]
    public string? ExpiresOn { get; set; }

    public bool? Sealed { get; set; }
    public string? RehydratePriority { get; set; }
    public string? LastAccessTime { get; set; }
    public string? ImmutabilityPolicyUntilDate { get; set; }
    public ImmutabilityPolicyMode? ImmutabilityPolicyMode { get; set; }
    public bool? LegalHold { get; set; }
}

public sealed class BlobMetadata
{
    [XmlAttribute]
    public string? Encrypted { get; set; }

    [XmlAnyElement]
    public XmlElement[] Items { get; set; } = [];
}

[XmlRoot("Tags")]
public sealed class BlobTags
{
    [XmlArray("TagSet")]
    [XmlArrayItem("Tag")]
    public BlobTag[] TagSet { get; set; } = [];
}

public sealed class BlobTag
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class ObjectReplicationMetadata
{
    [XmlAnyElement]
    public XmlElement[] Items { get; set; } = [];
}

public enum BlobType
{
    BlockBlob,
    PageBlob,
    AppendBlob,
}

public enum LeaseStatus
{
    [XmlEnum("unlocked")]
    Unlocked,
    [XmlEnum("locked")]
    Locked,
}

public enum LeaseState
{
    [XmlEnum("available")]
    Available,
    [XmlEnum("leased")]
    Leased,
    [XmlEnum("expired")]
    Expired,
    [XmlEnum("breaking")]
    Breaking,
    [XmlEnum("broken")]
    Broken,
}

public enum LeaseDuration
{
    [XmlEnum("infinite")]
    Infinite,
    [XmlEnum("fixed")]
    Fixed,
}

public enum CopyStatus
{
    [XmlEnum("pending")]
    Pending,
    [XmlEnum("success")]
    Success,
    [XmlEnum("failed")]
    Failed,
    [XmlEnum("aborted")]
    Aborted,
}

public enum ImmutabilityPolicyMode
{
    [XmlEnum("mutable")]
    Mutable,
    [XmlEnum("locked")]
    Locked,
    [XmlEnum("unlocked")]
    Unlocked,
}
