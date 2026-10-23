// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Xml.Linq;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class StorageFileXml
{
    internal sealed record Page(IReadOnlyList<StorageFileEntry> Entries, string? NextMarker);

    internal static async Task<Page> ReadPageAsync(HttpContent content, StorageFileDirectory directory, CancellationToken token)
    {
        var xml = await StorageWireFormat.ReadXmlAsync(content, token);
        if (xml.Name != "EnumerationResults" || string.IsNullOrEmpty((string?)xml.Attribute("ServiceEndpoint")) ||
            (string?)xml.Attribute("ShareName") != directory.Share.Name)
            throw StorageWireFormat.InvalidXml();
        string path = StorageWireFormat.DecodeXmlValue((string?)xml.Attribute("DirectoryPath") ?? throw StorageWireFormat.InvalidXml(),
            Boolean((string?)xml.Attribute("Encoded")) == true);
        string? snapshot = (string?)xml.Attribute("ShareSnapshot");
        if (path.Trim('/') != directory.Path || (string.IsNullOrEmpty(snapshot) ? null : snapshot) != directory.Share.ShareSnapshot)
            throw StorageWireFormat.InvalidXml();
        var entries = new List<StorageFileEntry>();
        foreach (var element in Required(xml, "Entries").Elements())
        {
            bool isDirectory = element.Name == "Directory";
            if (!isDirectory && element.Name != "File") throw StorageWireFormat.InvalidXml();
            var nameElement = Required(element, "Name");
            if (nameElement.HasElements) throw StorageWireFormat.InvalidXml();
            string name = StorageWireFormat.DecodeXmlValue(nameElement.Value, Boolean((string?)nameElement.Attribute("Encoded")) == true);
            if (name.Length == 0 || name is "." or ".." || name.Contains('/') || name.Contains('\\'))
                throw StorageWireFormat.InvalidXml();
            string childPath = directory.Path.Length == 0 ? name : directory.Path + "/" + name;
            var properties = Element(element, "Properties");
            long? length = properties is null ? null : Number(Text(properties, "Content-Length"));
            if (!isDirectory && length is null) throw StorageWireFormat.InvalidXml();
            var values = new StorageFileProperties
            {
                ContentLength = isDirectory ? null : length, FileId = Text(element, "FileId"), ParentId = Text(xml, "DirectoryId"),
                Attributes = Text(element, "Attributes"), PermissionKey = Text(element, "PermissionKey"),
                CreationTime = Date(properties, "CreationTime"), LastAccessTime = Date(properties, "LastAccessTime"),
                LastWriteTime = Date(properties, "LastWriteTime"), ChangeTime = Date(properties, "ChangeTime"),
                LastModified = Date(properties, "Last-Modified"), ETag = properties is null ? null : Text(properties, "Etag")
            };
            StorageFileEntry entry = isDirectory ? new StorageFileDirectory(childPath, directory.Share) : new StorageFile(childPath, directory.Share);
            _ = entry.Uri;
            entries.Add(entry with { Properties = values });
        }
        _ = Required(xml, "NextMarker");
        return new Page(entries, Text(xml, "NextMarker") is { Length: > 0 } marker ? marker : null);
    }

    private static XElement? Element(XElement xml, string name)
    {
        var elements = xml.Elements(name).Take(2).ToArray();
        return elements.Length <= 1 ? elements.FirstOrDefault() : throw StorageWireFormat.InvalidXml();
    }
    private static XElement Required(XElement xml, string name) => Element(xml, name) ?? throw StorageWireFormat.InvalidXml();
    private static string? Text(XElement xml, string name)
    {
        var element = Element(xml, name);
        return element?.HasElements == true ? throw StorageWireFormat.InvalidXml() : element?.Value;
    }
    private static DateTimeOffset? Date(XElement? xml, string name) => xml is null ? null : StorageFileProperties.Date(Text(xml, name));
    private static long? Number(string? value) => value is null ? null :
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ? parsed : throw StorageWireFormat.InvalidXml();
    private static bool? Boolean(string? value) => value switch
    {
        null => null, "true" or "1" => true, "false" or "0" => false, _ => throw StorageWireFormat.InvalidXml()
    };
}
