// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageBlob.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Get Blob Properties is a HEAD whose whole answer is in response headers, so the generated
    /// <see cref="GetPropertiesBlobProcessor"/> declares no response type and writes nothing. This
    /// processor sends it via <see cref="ProcessorBase.CreateExtendedProcessor{T}"/> and maps the spec's
    /// response headers onto <see cref="BlobItem"/>, the same shape List Blobs returns for each blob.
    /// </summary>
    public class GetPropertiesBlobProcessor_Orchestrator : ProcessorBase<object, BlobItem>, ISasAuthorized
    {
        private const string MetadataPrefix = "x-ms-meta-";
        private const string ObjectReplicationPrefix = "x-ms-or-";
        private const string ObjectReplicationPolicyId = "x-ms-or-policy-id";

        /// <summary>Container name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ContainerName { get; set; }

        /// <summary>Blob name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? BlobName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ContainerName), ContainerName), (nameof(BlobName), BlobName));

        public string? SasToken { get; set; }
        public string? Snapshot { get; set; }
        public string? VersionId { get; set; }
        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }
        public string? LeaseId { get; set; }
        public string? EncryptionKey { get; set; }
        public string? EncryptionKeySha256 { get; set; }
        public string? EncryptionAlgorithm { get; set; }
        public string? IfModifiedSince { get; set; }
        public string? IfUnmodifiedSince { get; set; }
        public string? IfNoneMatch { get; set; }
        public string? IfMatch { get; set; }
        public string? IfTags { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                using HttpResponseMessage response = await CreateExtendedProcessor<GetPropertiesBlobProcessor>(p =>
                {
                    p.Url = Endpoint;
                    p.Snapshot = Snapshot;
                    p.VersionId = VersionId;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                    p.LeaseId = LeaseId;
                    p.EncryptionKey = EncryptionKey;
                    p.EncryptionKeySha256 = EncryptionKeySha256;
                    p.EncryptionAlgorithm = EncryptionAlgorithm;
                    p.IfModifiedSince = IfModifiedSince;
                    p.IfUnmodifiedSince = IfUnmodifiedSince;
                    p.IfNoneMatch = IfNoneMatch;
                    p.IfMatch = IfMatch;
                    p.IfTags = IfTags;
                }).SendAsync();
                await WriteOutput(ToBlobItem(response));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        private BlobItem ToBlobItem(HttpResponseMessage response)
        {
            Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers.Concat(response.Content.Headers))
            {
                headers[header.Key] = string.Join(",", header.Value);
            }

            string? Text(string name) => headers.TryGetValue(name, out string? value) && value.Length > 0 ? value : null;
            bool? Bool(string name) => Text(name) is { } value ? bool.Parse(value) : null;
            int? Int(string name) => Text(name) is { } value ? int.Parse(value, CultureInfo.InvariantCulture) : null;
            long? Long(string name) => Text(name) is { } value ? long.Parse(value, CultureInfo.InvariantCulture) : null;

            Dictionary<string, object> metadata = headers
                .Where(h => h.Key.StartsWith(MetadataPrefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key[MetadataPrefix.Length..], h => (object)h.Value);
            Dictionary<string, string> objectReplication = headers
                .Where(h => h.Key.StartsWith(ObjectReplicationPrefix, StringComparison.OrdinalIgnoreCase)
                    && !h.Key.Equals(ObjectReplicationPolicyId, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key[ObjectReplicationPrefix.Length..], h => h.Value);

            return new BlobItem
            {
                Name = new BlobName { Content = BlobNameFromUrl() },
                Snapshot = Snapshot!,
                VersionId = Text("x-ms-version-id"),
                IsCurrentVersion = Bool("x-ms-is-current-version"),
                Metadata = metadata.Count > 0 ? new BlobMetadata { AdditionalProperties = metadata } : null,
                ObjectReplicationMetadata = objectReplication.Count > 0 ? objectReplication : null,
                Properties = new BlobProperties
                {
                    CreationTime = Text("x-ms-creation-time"),
                    LastModified = Text("Last-Modified")!,
                    ETag = Text("ETag")!,
                    ContentLength = Long("Content-Length"),
                    ContentType = Text("Content-Type"),
                    ContentEncoding = Text("Content-Encoding"),
                    ContentLanguage = Text("Content-Language"),
                    ContentMd5 = Text("Content-MD5") is { } md5 ? Convert.FromBase64String(md5) : null,
                    ContentDisposition = Text("Content-Disposition"),
                    CacheControl = Text("Cache-Control"),
                    BlobSequenceNumber = Long("x-ms-blob-sequence-number"),
                    BlobType = Text("x-ms-blob-type"),
                    LeaseStatus = Text("x-ms-lease-status"),
                    LeaseState = Text("x-ms-lease-state"),
                    LeaseDuration = Text("x-ms-lease-duration"),
                    CopyId = Text("x-ms-copy-id"),
                    CopyStatus = Text("x-ms-copy-status"),
                    CopySource = Text("x-ms-copy-source"),
                    CopyProgress = Text("x-ms-copy-progress"),
                    CopyCompletionTime = Text("x-ms-copy-completion-time"),
                    CopyStatusDescription = Text("x-ms-copy-status-description"),
                    ServerEncrypted = Bool("x-ms-server-encrypted"),
                    IncrementalCopy = Bool("x-ms-incremental-copy"),
                    DestinationSnapshot = Text("x-ms-copy-destination-snapshot"),
                    AccessTier = Text("x-ms-access-tier"),
                    AccessTierInferred = Bool("x-ms-access-tier-inferred"),
                    ArchiveStatus = Text("x-ms-archive-status"),
                    SmartAccessTier = Text("x-ms-smart-access-tier"),
                    CustomerProvidedKeySha256 = Text("x-ms-encryption-key-sha256"),
                    EncryptionScope = Text("x-ms-encryption-scope"),
                    AccessTierChangeTime = Text("x-ms-access-tier-change-time"),
                    TagCount = Int("x-ms-tag-count"),
                    ExpiresOn = Text("x-ms-expiry-time"),
                    IsSealed = Bool("x-ms-blob-sealed"),
                    RehydratePriority = Text("x-ms-rehydrate-priority"),
                    LastAccessedOn = Text("x-ms-last-access-time"),
                    ImmutabilityPolicyExpiresOn = Text("x-ms-immutability-policy-until-date"),
                    ImmutabilityPolicyMode = Text("x-ms-immutability-policy-mode"),
                    LegalHold = Bool("x-ms-legal-hold"),
                },
            };
        }

        /// <summary>The blob name is the URL path after the container segment; a HEAD response does not echo it.</summary>
        private string? BlobNameFromUrl()
        {
            string[] segments = new Uri(Endpoint).AbsolutePath.TrimStart('/').Split('/', 2);
            return segments.Length == 2 && segments[1].Length > 0 ? Uri.UnescapeDataString(segments[1]) : null;
        }
    }
}
