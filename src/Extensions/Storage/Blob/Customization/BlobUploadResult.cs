// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Models
{
    /// <summary>The result of an upload. The spec defines it only as response headers; these identify the written blob.</summary>
    public class BlobUploadResult
    {
        /// <summary>The blob ETag.</summary>
        [JsonPropertyName("eTag")]
        public string? ETag { get; set; }

        /// <summary>The blob's last-modified date-time.</summary>
        [JsonPropertyName("lastModified")]
        public string? LastModified { get; set; }

        /// <summary>The version ID of the blob, when versioning is enabled.</summary>
        [JsonPropertyName("versionId")]
        public string? VersionId { get; set; }

        internal static BlobUploadResult From(HttpResponseMessage response)
        {
            Func<string, string?> header = Processors.BlobWireFormat.ReadHeaders(response);
            return new BlobUploadResult
            {
                ETag = header("ETag"),
                LastModified = header("Last-Modified"),
                VersionId = header("x-ms-version-id"),
            };
        }
    }
}
