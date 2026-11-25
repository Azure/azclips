// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Models
{
    /// <summary>The result of Create Container. The spec defines it only as response headers; these identify the new container.</summary>
    public class ContainerCreateResult
    {
        /// <summary>The container ETag.</summary>
        [JsonPropertyName("eTag")]
        public string? ETag { get; set; }

        /// <summary>The container's last-modified date-time.</summary>
        [JsonPropertyName("lastModified")]
        public string? LastModified { get; set; }

        internal static ContainerCreateResult From(HttpResponseMessage response)
        {
            Func<string, string?> header = Processors.BlobWireFormat.ReadHeaders(response);
            return new ContainerCreateResult
            {
                ETag = header("ETag"),
                LastModified = header("Last-Modified"),
            };
        }
    }
}
