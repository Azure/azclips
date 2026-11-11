// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Models
{
    /// <summary>
    /// A directory in a share, returned by Create Directory, Get Directory Properties and the directory listing.
    /// </summary>
    public class ShareDirectory
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("fileId")]
        public string? FileId { get; set; }

        [JsonPropertyName("attributes")]
        public string? Attributes { get; set; }

        [JsonPropertyName("permissionKey")]
        public string? PermissionKey { get; set; }

        [JsonPropertyName("linkCount")]
        public long? LinkCount { get; set; }

        [JsonPropertyName("creationTime")]
        public string? CreationTime { get; set; }

        [JsonPropertyName("lastAccessTime")]
        public string? LastAccessTime { get; set; }

        [JsonPropertyName("lastWriteTime")]
        public string? LastWriteTime { get; set; }

        [JsonPropertyName("changeTime")]
        public string? ChangeTime { get; set; }

        [JsonPropertyName("lastModified")]
        public string? LastModified { get; set; }

        [JsonPropertyName("eTag")]
        public string? ETag { get; set; }

        /// <summary>NFS only.</summary>
        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        /// <summary>NFS only.</summary>
        [JsonPropertyName("group")]
        public string? Group { get; set; }

        /// <summary>NFS only.</summary>
        [JsonPropertyName("fileMode")]
        public string? FileMode { get; set; }

        /// <summary>Returned by Get Directory Properties only.</summary>
        [JsonPropertyName("metadata")]
        public IDictionary<string, string>? Metadata { get; set; }

        /// <summary>
        /// A directory from the response headers of Create Directory or Get Directory Properties. The response does
        /// not echo the path, so the name is the last segment of the request URL.
        /// </summary>
        internal static ShareDirectory FromHeaders(HttpResponseMessage response, string directoryUrl)
        {
            IReadOnlyDictionary<string, string> headers = Processors.FileWireFormat.ReadHeaders(response);
            string? Text(string name) => headers.TryGetValue(name, out string? value) && value.Length > 0 ? value : null;

            return new ShareDirectory
            {
                Name = Uri.UnescapeDataString(new Uri(directoryUrl).AbsolutePath.TrimEnd('/').Split('/')[^1]),
                FileId = Text("x-ms-file-id"),
                Attributes = Text("x-ms-file-attributes"),
                PermissionKey = Text("x-ms-file-permission-key"),
                CreationTime = Text("x-ms-file-creation-time"),
                LastWriteTime = Text("x-ms-file-last-write-time"),
                ChangeTime = Text("x-ms-file-change-time"),
                LastModified = Text("Last-Modified"),
                ETag = Text("ETag"),
                Owner = Text("x-ms-owner"),
                Group = Text("x-ms-group"),
                FileMode = Text("x-ms-mode"),
                Metadata = Processors.FileWireFormat.ReadMetadata(headers),
            };
        }
    }
}
