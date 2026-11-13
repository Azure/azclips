// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Models
{
    /// <summary>
    /// The result of Start Copy From URL. The spec defines it only as response headers; these are the ones
    /// that identify and track the pending copy (Azure CLI's <c>copy start</c> returns the same set).
    /// </summary>
    public class BlobCopyResult
    {
        /// <summary>The copy ID, used to poll the copy or abort it.</summary>
        [JsonPropertyName("copyId")]
        public string? CopyId { get; set; }

        /// <summary>The copy state: <c>pending</c>, or <c>success</c> when the service finished it synchronously.</summary>
        [JsonPropertyName("copyStatus")]
        public string? CopyStatus { get; set; }

        /// <summary>The destination blob ETag.</summary>
        [JsonPropertyName("eTag")]
        public string? ETag { get; set; }

        /// <summary>The destination blob's last-modified date-time.</summary>
        [JsonPropertyName("lastModified")]
        public string? LastModified { get; set; }

        /// <summary>The version ID of the destination blob, when versioning is enabled.</summary>
        [JsonPropertyName("versionId")]
        public string? VersionId { get; set; }
    }
}
