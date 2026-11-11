// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Xml;
using System.Xml.Linq;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Lists the directories and files directly under a directory, or under the share root when
    /// <see cref="DirectoryPath"/> is not set.
    /// <list type="bullet">
    /// <item>The response is XML (<c>EnumerationResults</c>), mapped by hand using the spec's <c>@Xml.name</c> element names.</item>
    /// <item>The spec declares no <c>@pageItems</c> (entries are split across per-kind arrays), so no paging is generated;
    /// <c>NextMarker</c> is followed here, and each page's directories and files are written as <see cref="DirectoryEntry"/>.</item>
    /// <item>NFS-only kinds (symbolic links, devices, FIFOs, sockets) are not listed.</item>
    /// </list>
    /// </summary>
    public partial class ListFilesAndDirectoriesSegmentDirectoryProcessor : ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        /// <summary>Directory path within the share; the share root when not set.</summary>
        public string? DirectoryPath { get; set; }

        protected override string Endpoint => DirectoryPath?.Trim('/') is { Length: > 0 } path
            ? ResolveUrl((nameof(ShareName), ShareName), (nameof(DirectoryPath), path))
            : ResolveUrl((nameof(ShareName), ShareName));

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(ListFilesAndDirectoriesSegmentResponse? body)
        {
            await body.WriteOutputWithPaginationAsync(Entries, page => page.NextMarker, token => Marker = token, CreateRequestMessage, DeserializeResponseAsync, WriteOutput, Publisher, HttpPipeline);
            return true;
        }

        private static IEnumerable<DirectoryEntry> Entries(ListFilesAndDirectoriesSegmentResponse page) =>
            page.Segment.DirectoryItems.Select(d => ToEntry(DirectoryEntry.DirectoryType, d.Name, d.FileId, d.Properties, d.Attributes, d.PermissionKey, d.LinkCount))
                .Concat(page.Segment.FileItems.Select(f => ToEntry(DirectoryEntry.FileType, f.Name, f.FileId, f.Properties, f.Attributes, f.PermissionKey, f.LinkCount)));

        private static DirectoryEntry ToEntry(string type, StringEncoded name, string? fileId, FileProperty? properties, string? attributes, string? permissionKey, long? linkCount) => new()
        {
            Name = name.Encoded == true ? Uri.UnescapeDataString(name.Content ?? string.Empty) : name.Content ?? string.Empty,
            Type = type,
            FileId = fileId,
            ContentLength = type == DirectoryEntry.FileType ? properties?.ContentLength : null,
            Attributes = attributes,
            PermissionKey = permissionKey,
            LinkCount = linkCount,
            CreationTime = properties?.CreationTime,
            LastAccessTime = properties?.LastAccessTime,
            LastWriteTime = properties?.LastWriteTime,
            ChangeTime = properties?.ChangeTime,
            LastModified = properties?.LastModified,
            ETag = properties?.ETag,
            Owner = properties?.Owner,
            Group = properties?.Group,
            FileMode = properties?.FileMode,
        };

        protected override Task<ListFilesAndDirectoriesSegmentResponse?> DeserializeResponseAsync(string rawBody)
        {
            XElement root = XDocument.Parse(rawBody).Root!;
            XElement? entries = root.Element("Entries");
            ListFilesAndDirectoriesSegmentResponse response = new()
            {
                ServiceEndpoint = root.Attribute("ServiceEndpoint")?.Value!,
                ShareName = root.Attribute("ShareName")?.Value!,
                ShareSnapshot = root.Attribute("ShareSnapshot")?.Value,
                DirectoryPath = root.Attribute("DirectoryPath")?.Value!,
                Prefix = ToStringEncoded(root.Element("Prefix")),
                Marker = Text(root, "Marker"),
                MaxResults = Text(root, "MaxResults") is { } max ? XmlConvert.ToInt32(max) : null,
                DirectoryId = Text(root, "DirectoryId"),
                NextMarker = Text(root, "NextMarker")!,
                Segment = new FilesAndDirectoriesListSegment
                {
                    DirectoryItems = entries?.Elements("Directory").Select(d => new DirectoryItem
                    {
                        Name = ToStringEncoded(d.Element("Name")),
                        FileId = Text(d, "FileId"),
                        Properties = d.Element("Properties") is { } p ? ToFileProperty(p) : null,
                        Attributes = Text(d, "Attributes"),
                        PermissionKey = Text(d, "PermissionKey"),
                        LinkCount = Long(d, "LinkCount"),
                    }).ToList() ?? [],
                    FileItems = entries?.Elements("File").Select(f => new FileItem
                    {
                        Name = ToStringEncoded(f.Element("Name")),
                        FileId = Text(f, "FileId"),
                        Properties = ToFileProperty(f.Element("Properties")),
                        Attributes = Text(f, "Attributes"),
                        PermissionKey = Text(f, "PermissionKey"),
                        LinkCount = Long(f, "LinkCount"),
                    }).ToList() ?? [],
                },
            };
            return Task.FromResult<ListFilesAndDirectoriesSegmentResponse?>(response);
        }

        private static FileProperty ToFileProperty(XElement? properties) => new()
        {
            ContentLength = Long(properties, "Content-Length") ?? 0,
            CreationTime = Text(properties, "CreationTime"),
            LastAccessTime = Text(properties, "LastAccessTime"),
            LastWriteTime = Text(properties, "LastWriteTime"),
            ChangeTime = Text(properties, "ChangeTime"),
            LastModified = Text(properties, "Last-Modified"),
            ETag = Text(properties, "Etag"),
            Owner = Text(properties, "Uid"),
            Group = Text(properties, "Gid"),
            FileMode = Text(properties, "Mode"),
        };

        /// <summary>A name the service percent-encodes (<c>Encoded="true"</c>) when it holds characters invalid in XML.</summary>
        private static StringEncoded ToStringEncoded(XElement? element) => new()
        {
            Encoded = element?.Attribute("Encoded")?.Value is { } encoded ? XmlConvert.ToBoolean(encoded) : null,
            Content = element?.Value,
        };

        /// <summary>The element's text, or null when it is absent or empty (the service emits e.g. <c>&lt;NextMarker /&gt;</c>).</summary>
        private static string? Text(XElement? parent, string name) =>
            parent?.Element(name)?.Value is { Length: > 0 } value ? value : null;

        private static long? Long(XElement? parent, string name) =>
            Text(parent, name) is { } value ? XmlConvert.ToInt64(value) : null;
    }
}
