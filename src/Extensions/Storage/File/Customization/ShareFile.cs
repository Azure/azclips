// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Models
{
    /// <summary>
    /// A file in a share, returned by Create File and Get File Properties. Both answer in headers only, so this maps the
    /// spec's response headers; Create File returns a subset of them.
    /// </summary>
    public class ShareFile
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>File size in bytes.</summary>
        [JsonPropertyName("contentLength")]
        public long? ContentLength { get; set; }

        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; }

        [JsonPropertyName("contentEncoding")]
        public string? ContentEncoding { get; set; }

        [JsonPropertyName("contentLanguage")]
        public string? ContentLanguage { get; set; }

        [JsonPropertyName("contentDisposition")]
        public string? ContentDisposition { get; set; }

        [JsonPropertyName("cacheControl")]
        public string? CacheControl { get; set; }

        /// <summary>Base64 MD5 of the whole file, when one was set.</summary>
        [JsonPropertyName("contentMd5")]
        public string? ContentMd5 { get; set; }

        [JsonPropertyName("fileId")]
        public string? FileId { get; set; }

        [JsonPropertyName("parentId")]
        public string? ParentId { get; set; }

        [JsonPropertyName("attributes")]
        public string? Attributes { get; set; }

        [JsonPropertyName("permissionKey")]
        public string? PermissionKey { get; set; }

        [JsonPropertyName("creationTime")]
        public string? CreationTime { get; set; }

        [JsonPropertyName("lastWriteTime")]
        public string? LastWriteTime { get; set; }

        [JsonPropertyName("changeTime")]
        public string? ChangeTime { get; set; }

        [JsonPropertyName("lastModified")]
        public string? LastModified { get; set; }

        [JsonPropertyName("eTag")]
        public string? ETag { get; set; }

        [JsonPropertyName("serverEncrypted")]
        public bool? ServerEncrypted { get; set; }

        [JsonPropertyName("leaseStatus")]
        public string? LeaseStatus { get; set; }

        [JsonPropertyName("leaseState")]
        public string? LeaseState { get; set; }

        [JsonPropertyName("leaseDuration")]
        public string? LeaseDuration { get; set; }

        [JsonPropertyName("copyId")]
        public string? CopyId { get; set; }

        [JsonPropertyName("copyStatus")]
        public string? CopyStatus { get; set; }

        [JsonPropertyName("copySource")]
        public string? CopySource { get; set; }

        [JsonPropertyName("copyProgress")]
        public string? CopyProgress { get; set; }

        [JsonPropertyName("copyCompletionTime")]
        public string? CopyCompletionTime { get; set; }

        [JsonPropertyName("copyStatusDescription")]
        public string? CopyStatusDescription { get; set; }

        /// <summary>NFS only.</summary>
        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        /// <summary>NFS only.</summary>
        [JsonPropertyName("group")]
        public string? Group { get; set; }

        /// <summary>NFS only.</summary>
        [JsonPropertyName("fileMode")]
        public string? FileMode { get; set; }

        /// <summary>NFS only.</summary>
        [JsonPropertyName("linkCount")]
        public long? LinkCount { get; set; }

        [JsonPropertyName("metadata")]
        public IDictionary<string, string>? Metadata { get; set; }

        /// <summary>A file from response headers. The response does not echo the path, so the name is the last segment of the request URL.</summary>
        internal static ShareFile FromHeaders(HttpResponseMessage response, string fileUrl)
        {
            IReadOnlyDictionary<string, string> headers = Processors.FileWireFormat.ReadHeaders(response);
            string? Text(string name) => headers.TryGetValue(name, out string? value) && value.Length > 0 ? value : null;
            long? Long(string name) => Text(name) is { } value ? long.Parse(value, CultureInfo.InvariantCulture) : null;

            return new ShareFile
            {
                Name = Uri.UnescapeDataString(new Uri(fileUrl).AbsolutePath.TrimEnd('/').Split('/')[^1]),
                ContentLength = Long("Content-Length"),
                ContentType = Text("Content-Type"),
                ContentEncoding = Text("Content-Encoding"),
                ContentLanguage = Text("Content-Language"),
                ContentDisposition = Text("Content-Disposition"),
                CacheControl = Text("Cache-Control"),
                ContentMd5 = Text("Content-MD5"),
                FileId = Text("x-ms-file-id"),
                ParentId = Text("x-ms-file-parent-id"),
                Attributes = Text("x-ms-file-attributes"),
                PermissionKey = Text("x-ms-file-permission-key"),
                CreationTime = Text("x-ms-file-creation-time"),
                LastWriteTime = Text("x-ms-file-last-write-time"),
                ChangeTime = Text("x-ms-file-change-time"),
                LastModified = Text("Last-Modified"),
                ETag = Text("ETag"),
                ServerEncrypted = Text("x-ms-server-encrypted") is { } encrypted ? bool.Parse(encrypted) : null,
                LeaseStatus = Text("x-ms-lease-status"),
                LeaseState = Text("x-ms-lease-state"),
                LeaseDuration = Text("x-ms-lease-duration"),
                CopyId = Text("x-ms-copy-id"),
                CopyStatus = Text("x-ms-copy-status"),
                CopySource = Text("x-ms-copy-source"),
                CopyProgress = Text("x-ms-copy-progress"),
                CopyCompletionTime = Text("x-ms-copy-completion-time"),
                CopyStatusDescription = Text("x-ms-copy-status-description"),
                Owner = Text("x-ms-owner"),
                Group = Text("x-ms-group"),
                FileMode = Text("x-ms-mode"),
                LinkCount = Long("x-ms-link-count"),
                Metadata = Processors.FileWireFormat.ReadMetadata(headers),
            };
        }
    }
}
