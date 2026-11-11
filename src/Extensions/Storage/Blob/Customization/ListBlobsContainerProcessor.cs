// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Xml.Linq;
using Microsoft.Azure.AzClips.Extensions.StorageBlob.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// List Blobs answers in XML (<c>EnumerationResults</c>), while the generated models carry only JSON names,
    /// so the response is mapped by hand using the spec's <c>@Xml.name</c> element names.
    /// </summary>
    public partial class ListBlobsContainerProcessor : ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Container name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ContainerName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ContainerName), ContainerName));

        protected override Task<ListBlobsResponse?> DeserializeResponseAsync(string rawBody)
        {
            XElement root = XDocument.Parse(rawBody).Root!;
            ListBlobsResponse response = new()
            {
                ServiceEndpoint = root.Attribute("ServiceEndpoint")?.Value!,
                ContainerName = root.Attribute("ContainerName")?.Value!,
                Prefix = Text(root, "Prefix"),
                Marker = Text(root, "Marker"),
                MaxResults = Int(root, "MaxResults"),
                BlobItems = root.Element("Blobs")?.Elements("Blob").Select(ToBlobItem).ToList() ?? [],
                NextMarker = Text(root, "NextMarker"),
            };
            return Task.FromResult<ListBlobsResponse?>(response);
        }

        private static BlobItem ToBlobItem(XElement blob)
        {
            XElement? name = blob.Element("Name");
            XElement? properties = blob.Element("Properties");
            XElement? metadata = blob.Element("Metadata");
            XElement? tagSet = blob.Element("Tags")?.Element("TagSet");

            return new BlobItem
            {
                Name = new BlobName
                {
                    Encoded = name?.Attribute("Encoded") is { } encoded ? XmlConvert.ToBoolean(encoded.Value) : null,
                    Content = name?.Value,
                },
                Deleted = Bool(blob, "Deleted") ?? false,
                Snapshot = Text(blob, "Snapshot")!,
                VersionId = Text(blob, "VersionId"),
                IsCurrentVersion = Bool(blob, "IsCurrentVersion"),
                Properties = ToBlobProperties(properties),
                Metadata = metadata is null ? null : new BlobMetadata { Encrypted = metadata.Attribute("Encrypted")?.Value },
                BlobTags = tagSet is null ? null : new BlobTags
                {
                    BlobTagSet = tagSet.Elements("Tag")
                        .Select(tag => new BlobTag { Key = Text(tag, "Key")!, Value = Text(tag, "Value") ?? string.Empty })
                        .ToList(),
                },
                HasVersionsOnly = Bool(blob, "HasVersionsOnly"),
            };
        }

        private static BlobProperties ToBlobProperties(XElement? properties) => new()
        {
            CreationTime = Text(properties, "Creation-Time"),
            LastModified = Text(properties, "Last-Modified")!,
            ETag = Text(properties, "Etag")!,
            ContentLength = Long(properties, "Content-Length"),
            ContentType = Text(properties, "Content-Type"),
            ContentEncoding = Text(properties, "Content-Encoding"),
            ContentLanguage = Text(properties, "Content-Language"),
            ContentMd5 = Text(properties, "Content-MD5") is { } md5 ? Convert.FromBase64String(md5) : null,
            ContentDisposition = Text(properties, "Content-Disposition"),
            CacheControl = Text(properties, "Cache-Control"),
            BlobSequenceNumber = Long(properties, "x-ms-blob-sequence-number"),
            BlobType = Text(properties, "BlobType"),
            LeaseStatus = Text(properties, "LeaseStatus"),
            LeaseState = Text(properties, "LeaseState"),
            LeaseDuration = Text(properties, "LeaseDuration"),
            CopyId = Text(properties, "CopyId"),
            CopyStatus = Text(properties, "CopyStatus"),
            CopySource = Text(properties, "CopySource"),
            CopyProgress = Text(properties, "CopyProgress"),
            CopyCompletionTime = Text(properties, "CopyCompletionTime"),
            CopyStatusDescription = Text(properties, "CopyStatusDescription"),
            ServerEncrypted = Bool(properties, "ServerEncrypted"),
            IncrementalCopy = Bool(properties, "IncrementalCopy"),
            DestinationSnapshot = Text(properties, "DestinationSnapshot"),
            DeletedTime = Text(properties, "DeletedTime"),
            RemainingRetentionDays = Int(properties, "RemainingRetentionDays"),
            AccessTier = Text(properties, "AccessTier"),
            AccessTierInferred = Bool(properties, "AccessTierInferred"),
            ArchiveStatus = Text(properties, "ArchiveStatus"),
            SmartAccessTier = Text(properties, "SmartAccessTier"),
            CustomerProvidedKeySha256 = Text(properties, "CustomerProvidedKeySha256"),
            EncryptionScope = Text(properties, "EncryptionScope"),
            AccessTierChangeTime = Text(properties, "AccessTierChangeTime"),
            TagCount = Int(properties, "TagCount"),
            ExpiresOn = Text(properties, "Expiry-Time"),
            IsSealed = Bool(properties, "Sealed"),
            RehydratePriority = Text(properties, "RehydratePriority"),
            LastAccessedOn = Text(properties, "LastAccessTime"),
            ImmutabilityPolicyExpiresOn = Text(properties, "ImmutabilityPolicyUntilDate"),
            ImmutabilityPolicyMode = Text(properties, "ImmutabilityPolicyMode"),
            LegalHold = Bool(properties, "LegalHold"),
        };

        /// <summary>The element's text, or null when it is absent or empty (the service emits e.g. <c>&lt;NextMarker /&gt;</c>).</summary>
        private static string? Text(XElement? parent, string name) =>
            parent?.Element(name)?.Value is { Length: > 0 } value ? value : null;

        private static bool? Bool(XElement? parent, string name) =>
            Text(parent, name) is { } value ? XmlConvert.ToBoolean(value) : null;

        private static int? Int(XElement? parent, string name) =>
            Text(parent, name) is { } value ? XmlConvert.ToInt32(value) : null;

        private static long? Long(XElement? parent, string name) =>
            Text(parent, name) is { } value ? XmlConvert.ToInt64(value) : null;
    }
}
