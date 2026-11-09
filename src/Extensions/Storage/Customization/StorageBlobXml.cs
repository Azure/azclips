// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class StorageBlobXml
{
    internal sealed record Page(IReadOnlyList<StorageBlobEntry> Entries, string? NextMarker);

    internal static async Task<Page> ReadPageAsync(HttpContent content, StorageBlobContainer container,
        bool hierarchy, CancellationToken token)
    {
        var xml = await StorageWireFormat.ReadXmlAsync(content, token);
        ValidateRoot(xml);
        if ((string?)xml.Attribute("ContainerName") != container.Name)
            throw StorageWireFormat.InvalidXml();
        var entries = new List<StorageBlobEntry>();
        foreach (var element in RequiredElement(xml, "Blobs").Elements())
        {
            if (element.Name == "Blob")
                entries.Add(ToNative(ReadBlob(element), container));
            else if (hierarchy && element.Name == "BlobPrefix")
                entries.Add(new StorageBlobPrefix(DecodeName(ReadName(RequiredElement(element, "Name"))), container));
            else
                throw StorageWireFormat.InvalidXml();
        }
        return new Page(entries, Marker(xml));
    }

    internal static BlobModels.ListBlobsResponse ReadFlat(XElement xml)
    {
        ValidateRoot(xml);
        var blobs = RequiredElement(xml, "Blobs");
        if (blobs.Elements().Any(element => element.Name != "Blob")) throw StorageWireFormat.InvalidXml();
        return new BlobModels.ListBlobsResponse
        {
            ServiceEndpoint = RequiredAttribute(xml, "ServiceEndpoint"), ContainerName = RequiredAttribute(xml, "ContainerName"),
            Prefix = Text(xml, "Prefix"), Marker = Text(xml, "Marker"), MaxResults = Int(xml, "MaxResults"),
            BlobItems = blobs.Elements("Blob").Select(ReadBlob).ToList(), NextMarker = Marker(xml)
        };
    }

    internal static BlobModels.ListBlobsHierarchicalResponse ReadHierarchy(XElement xml)
    {
        ValidateRoot(xml);
        var blobs = RequiredElement(xml, "Blobs");
        if (blobs.Elements().Any(element => element.Name != "Blob" && element.Name != "BlobPrefix"))
            throw StorageWireFormat.InvalidXml();
        return new BlobModels.ListBlobsHierarchicalResponse
        {
            ServiceEndpoint = RequiredAttribute(xml, "ServiceEndpoint"), ContainerName = RequiredAttribute(xml, "ContainerName"),
            Prefix = Text(xml, "Prefix"), Delimiter = Text(xml, "Delimiter"), Marker = Text(xml, "Marker"),
            MaxResults = Int(xml, "MaxResults"), NextMarker = Marker(xml),
            HierarchicalList = new BlobModels.BlobHierarchyList
            {
                BlobItems = blobs.Elements("Blob").Select(ReadBlob).ToList(),
                BlobPrefixes = blobs.Elements("BlobPrefix").Select(element =>
                    new BlobModels.BlobPrefix { Name = ReadName(RequiredElement(element, "Name")) }).ToList()
            }
        };
    }

    private static BlobModels.BlobItem ReadBlob(XElement xml) => new()
    {
        Name = ReadName(RequiredElement(xml, "Name")),
        Deleted = Bool(xml, "Deleted") ?? false,
        Snapshot = Text(xml, "Snapshot") ?? string.Empty,
        VersionId = Text(xml, "VersionId"), IsCurrentVersion = Bool(xml, "IsCurrentVersion"),
        HasVersionsOnly = Bool(xml, "HasVersionsOnly"),
        Properties = ReadProperties(RequiredElement(xml, "Properties")),
        Metadata = xml.Element("Metadata") is { } metadata ? new BlobModels.BlobMetadata
        {
            Encrypted = (string?)metadata.Attribute("Encrypted"), AdditionalProperties = StringMap(metadata)
        } : null,
        BlobTags = xml.Element("Tags") is { } tags ? ReadTags(tags) : null,
        ObjectReplicationMetadata = xml.Element("OrMetadata") is { } replication
            ? new BlobModels.ObjectReplicationMetadata { AdditionalProperties = StringMap(replication) } : null
    };

    private static BlobModels.BlobProperties ReadProperties(XElement xml) => new()
    {
        CreationTime = Text(xml, "Creation-Time"), LastModified = Required(xml, "Last-Modified"), ETag = Required(xml, "Etag"),
        ContentLength = Long(xml, "Content-Length"), ContentType = Text(xml, "Content-Type"),
        ContentEncoding = Text(xml, "Content-Encoding"), ContentLanguage = Text(xml, "Content-Language"),
        ContentMd5 = string.IsNullOrEmpty(Text(xml, "Content-MD5")) ? null : Base64(Required(xml, "Content-MD5")),
        ContentDisposition = Text(xml, "Content-Disposition"), CacheControl = Text(xml, "Cache-Control"),
        BlobSequenceNumber = Long(xml, "x-ms-blob-sequence-number"), BlobType = Text(xml, "BlobType"),
        LeaseStatus = Text(xml, "LeaseStatus"), LeaseState = Text(xml, "LeaseState"), LeaseDuration = Text(xml, "LeaseDuration"),
        CopyId = Text(xml, "CopyId"), CopyStatus = Text(xml, "CopyStatus"),
        CopySource = StorageCopyAuthorization.PublicSource(Text(xml, "CopySource")),
        CopyProgress = Text(xml, "CopyProgress"), CopyCompletionTime = Text(xml, "CopyCompletionTime"),
        CopyStatusDescription = StorageCopyAuthorization.StatusDescription(Text(xml, "CopyStatusDescription")), ServerEncrypted = Bool(xml, "ServerEncrypted"),
        IncrementalCopy = Bool(xml, "IncrementalCopy"), DestinationSnapshot = Text(xml, "DestinationSnapshot"),
        DeletedTime = Text(xml, "DeletedTime"), RemainingRetentionDays = Int(xml, "RemainingRetentionDays"),
        AccessTier = Text(xml, "AccessTier"), AccessTierInferred = Bool(xml, "AccessTierInferred"),
        ArchiveStatus = Text(xml, "ArchiveStatus"), SmartAccessTier = Text(xml, "SmartAccessTier"),
        CustomerProvidedKeySha256 = Text(xml, "CustomerProvidedKeySha256"), EncryptionScope = Text(xml, "EncryptionScope"),
        AccessTierChangeTime = Text(xml, "AccessTierChangeTime"), TagCount = Int(xml, "TagCount"),
        ExpiresOn = Text(xml, "Expiry-Time"), IsSealed = Bool(xml, "Sealed"),
        RehydratePriority = Text(xml, "RehydratePriority"), LastAccessedOn = Text(xml, "LastAccessTime"),
        ImmutabilityPolicyExpiresOn = Text(xml, "ImmutabilityPolicyUntilDate"),
        ImmutabilityPolicyMode = Text(xml, "ImmutabilityPolicyMode"), LegalHold = Bool(xml, "LegalHold")
    };

    private static StorageBlob ToNative(BlobModels.BlobItem blob, StorageBlobContainer container) =>
        new(DecodeName(blob.Name), container, blob.Properties)
        {
            Snapshot = string.IsNullOrEmpty(blob.Snapshot) ? null : blob.Snapshot, VersionId = blob.VersionId,
            IsCurrentVersion = blob.IsCurrentVersion, Deleted = blob.Deleted, HasVersionsOnly = blob.HasVersionsOnly,
            Metadata = NativeMap(blob.Metadata?.AdditionalProperties),
            ObjectReplicationMetadata = NativeMap(blob.ObjectReplicationMetadata?.AdditionalProperties),
            Tags = new ReadOnlyDictionary<string, string>((blob.BlobTags?.BlobTagSet ?? []).ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal))
        };

    internal static BlobModels.BlobTags ReadTags(XElement xml)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        return new BlobModels.BlobTags
        {
            BlobTagSet = RequiredElement(xml, "TagSet").Elements("Tag").Select(tag =>
            {
                string key = Required(tag, "Key");
                if (!keys.Add(key)) throw StorageWireFormat.InvalidXml();
                return new BlobModels.BlobTag { Key = key, Value = RequiredElement(tag, "Value").Value };
            }).ToList()
        };
    }

    private static Dictionary<string, JsonElement> StringMap(XElement xml)
    {
        var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var element in xml.Elements())
        {
            if (element.HasElements || !map.TryAdd(element.Name.LocalName, JsonSerializer.SerializeToElement(element.Value)))
                throw StorageWireFormat.InvalidXml();
        }
        return map;
    }

    private static IReadOnlyDictionary<string, string> NativeMap(IDictionary<string, JsonElement>? map) => map is null
        ? StorageBlob.EmptyMap
        : new ReadOnlyDictionary<string, string>(map.ToDictionary(pair => pair.Key,
            pair => pair.Value.GetString() ?? throw StorageWireFormat.InvalidXml(), StringComparer.Ordinal));

    private static BlobModels.BlobName ReadName(XElement name) =>
        new() { Content = name.Value.Length > 0 ? name.Value : throw StorageWireFormat.InvalidXml(), Encoded = Boolean((string?)name.Attribute("Encoded")) };

    private static string DecodeName(BlobModels.BlobName name) =>
        StorageWireFormat.DecodeXmlValue(name.Content ?? throw StorageWireFormat.InvalidXml(), name.Encoded == true);

    private static void ValidateRoot(XElement xml)
    {
        if (xml.Name != "EnumerationResults") throw StorageWireFormat.InvalidXml();
        RequiredAttribute(xml, "ServiceEndpoint");
        RequiredAttribute(xml, "ContainerName");
    }

    private static XElement RequiredElement(XElement xml, string name) => xml.Element(name) ?? throw StorageWireFormat.InvalidXml();
    private static string Required(XElement xml, string name) =>
        Text(xml, name) is { Length: > 0 } value ? value : throw StorageWireFormat.InvalidXml();
    private static string RequiredAttribute(XElement xml, string name) =>
        (string?)xml.Attribute(name) is { Length: > 0 } value ? value : throw StorageWireFormat.InvalidXml();
    private static string? Text(XElement xml, string name) => (string?)xml.Element(name);
    private static string? Marker(XElement xml) => Text(xml, "NextMarker") is { Length: > 0 } value ? value : null;
    private static bool? Bool(XElement xml, string name) => Boolean(Text(xml, name));
    private static bool? Boolean(string? value) => value switch
    {
        null => null, "true" or "1" => true, "false" or "0" => false, _ => throw StorageWireFormat.InvalidXml()
    };
    private static long? Long(XElement xml, string name) => Text(xml, name) is { } value
        ? long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ? parsed : throw StorageWireFormat.InvalidXml()
        : null;
    private static int? Int(XElement xml, string name) => Long(xml, name) is { } value
        ? value <= int.MaxValue ? (int)value : throw StorageWireFormat.InvalidXml()
        : null;
    private static byte[] Base64(string value)
    {
        try { return Convert.FromBase64String(value); }
        catch (FormatException) { throw StorageWireFormat.InvalidXml(); }
    }
}
