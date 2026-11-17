// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Models
{
    /// <summary>The result of a download: where the content was written and which blob it came from.</summary>
    public class BlobDownloadResult
    {
        /// <summary>The local file the content was written to.</summary>
        [JsonPropertyName("file")]
        public string? File { get; set; }

        /// <summary>Bytes written.</summary>
        [JsonPropertyName("contentLength")]
        public long ContentLength { get; set; }

        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; }

        /// <summary>The blob ETag.</summary>
        [JsonPropertyName("eTag")]
        public string? ETag { get; set; }

        /// <summary>The blob's last-modified date-time.</summary>
        [JsonPropertyName("lastModified")]
        public string? LastModified { get; set; }

        /// <summary>The version ID of the blob, when versioning is enabled.</summary>
        [JsonPropertyName("versionId")]
        public string? VersionId { get; set; }

        internal static BlobDownloadResult From(HttpResponseMessage response, string file, long contentLength)
        {
            Func<string, string?> header = Processors.BlobWireFormat.ReadHeaders(response);
            return new BlobDownloadResult
            {
                File = file,
                ContentLength = contentLength,
                ContentType = header("Content-Type"),
                ETag = header("ETag"),
                LastModified = header("Last-Modified"),
                VersionId = header("x-ms-version-id"),
            };
        }
    }
}
