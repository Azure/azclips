// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Models
{
    /// <summary>The result of Create Share. The spec defines it only as response headers.</summary>
    public class ShareCreateResult
    {
        [JsonPropertyName("eTag")]
        public string? ETag { get; set; }

        [JsonPropertyName("lastModified")]
        public string? LastModified { get; set; }

        /// <summary>The share quota in GiB.</summary>
        [JsonPropertyName("quota")]
        public int? Quota { get; set; }

        [JsonPropertyName("provisionedIops")]
        public long? ProvisionedIops { get; set; }

        [JsonPropertyName("provisionedBandwidthMibps")]
        public long? ProvisionedBandwidthMibps { get; set; }

        [JsonPropertyName("includedBurstIops")]
        public long? IncludedBurstIops { get; set; }

        [JsonPropertyName("maxBurstCreditsForIops")]
        public long? MaxBurstCreditsForIops { get; set; }

        internal static ShareCreateResult From(HttpResponseMessage response)
        {
            IReadOnlyDictionary<string, string> headers = Processors.FileWireFormat.ReadHeaders(response);
            string? Text(string name) => headers.TryGetValue(name, out string? value) && value.Length > 0 ? value : null;
            long? Long(string name) => Text(name) is { } value ? long.Parse(value, CultureInfo.InvariantCulture) : null;

            return new ShareCreateResult
            {
                ETag = Text("ETag"),
                LastModified = Text("Last-Modified"),
                Quota = Text("x-ms-share-quota") is { } quota ? int.Parse(quota, CultureInfo.InvariantCulture) : null,
                ProvisionedIops = Long("x-ms-share-provisioned-iops"),
                ProvisionedBandwidthMibps = Long("x-ms-share-provisioned-bandwidth-mibps"),
                IncludedBurstIops = Long("x-ms-share-included-burst-iops"),
                MaxBurstCreditsForIops = Long("x-ms-share-max-burst-credits-for-iops"),
            };
        }
    }
}
