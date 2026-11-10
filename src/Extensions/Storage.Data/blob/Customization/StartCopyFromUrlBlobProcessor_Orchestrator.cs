// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageBlob.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Start Copy From URL on top of the generated <see cref="StartCopyFromUrlBlobProcessor"/>, which needs
    /// hand-written wire formats the spec leaves to the client:
    /// <list type="bullet">
    /// <item><description>metadata is one <c>x-ms-meta-{key}</c> header per pair, not the single <c>x-ms-meta</c> the spec declares;</description></item>
    /// <item><description><c>x-ms-tags</c> is a URL-encoded query string;</description></item>
    /// <item><description><c>x-ms-copy-source</c> must be URL-encoded.</description></item>
    /// </list>
    /// The response is headers only and is mapped to <see cref="BlobCopyResult"/>.
    /// </summary>
    /// <remarks>
    /// The source is read by the service, not signed by this request: it must be public, in the destination
    /// account, or carry a SAS in <see cref="SourceUrl"/>.
    /// </remarks>
    public class StartCopyFromUrlBlobProcessor_Orchestrator : ProcessorBase<object, BlobCopyResult>, ISasAuthorized
    {
        /// <summary>SAS for the destination; a SAS for the source belongs in <see cref="SourceUrl"/>.</summary>
        public string? SasToken { get; set; }

        /// <summary>The source blob URL, unencoded or encoded; a SAS query string is kept as given.</summary>
        public string SourceUrl { get; set; } = string.Empty;

        /// <summary>Destination container name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ContainerName { get; set; }

        /// <summary>Destination blob name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? BlobName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ContainerName), ContainerName), (nameof(BlobName), BlobName));

        /// <summary>Destination metadata as <c>key=value</c> pairs.</summary>
        public string[]? Metadata { get; set; }

        /// <summary>Destination tags as <c>key=value</c> pairs.</summary>
        public string[]? Tags { get; set; }

        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }
        public string? Tier { get; set; }
        public string? RehydratePriority { get; set; }
        public string? SourceIfModifiedSince { get; set; }
        public string? SourceIfUnmodifiedSince { get; set; }
        public string? SourceIfMatch { get; set; }
        public string? SourceIfNoneMatch { get; set; }
        public string? SourceIfTags { get; set; }
        public string? IfModifiedSince { get; set; }
        public string? IfUnmodifiedSince { get; set; }
        public string? IfNoneMatch { get; set; }
        public string? IfMatch { get; set; }
        public string? IfTags { get; set; }
        public string? LeaseId { get; set; }
        public bool? SealBlob { get; set; }
        public string? ImmutabilityPolicyExpiry { get; set; }
        public string? ImmutabilityPolicyMode { get; set; }
        public bool? LegalHold { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                Dictionary<string, string> metadata = BlobWireFormat.ParsePairs(Metadata, nameof(Metadata));
                Dictionary<string, string> tags = BlobWireFormat.ParsePairs(Tags, nameof(Tags));
                using HttpResponseMessage response = await CreateExtendedProcessor<StartCopyRequest>(p =>
                {
                    p.Url = Endpoint;
                    p.CopySource = BlobWireFormat.EncodeUrl(SourceUrl, nameof(SourceUrl));
                    p.MetadataPairs = metadata;
                    p.BlobTagsString = BlobWireFormat.FormatTags(tags);
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                    p.Tier = Tier;
                    p.RehydratePriority = RehydratePriority;
                    p.SourceIfModifiedSince = SourceIfModifiedSince;
                    p.SourceIfUnmodifiedSince = SourceIfUnmodifiedSince;
                    p.SourceIfMatch = SourceIfMatch;
                    p.SourceIfNoneMatch = SourceIfNoneMatch;
                    p.SourceIfTags = SourceIfTags;
                    p.IfModifiedSince = IfModifiedSince;
                    p.IfUnmodifiedSince = IfUnmodifiedSince;
                    p.IfNoneMatch = IfNoneMatch;
                    p.IfMatch = IfMatch;
                    p.IfTags = IfTags;
                    p.LeaseId = LeaseId;
                    p.SealBlob = SealBlob;
                    p.ImmutabilityPolicyExpiry = ImmutabilityPolicyExpiry;
                    p.ImmutabilityPolicyMode = ImmutabilityPolicyMode;
                    p.LegalHold = LegalHold;
                }).SendAsync();
                await WriteOutput(ToResult(response));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        private static BlobCopyResult ToResult(HttpResponseMessage response)
        {
            Func<string, string?> header = BlobWireFormat.ReadHeaders(response);
            return new BlobCopyResult
            {
                CopyId = header("x-ms-copy-id"),
                CopyStatus = header("x-ms-copy-status"),
                ETag = header("ETag"),
                LastModified = header("Last-Modified"),
                VersionId = header("x-ms-version-id"),
            };
        }

        /// <summary>Sends metadata as one <c>x-ms-meta-{key}</c> header per pair instead of the generated single header.</summary>
        private sealed class StartCopyRequest : StartCopyFromUrlBlobProcessor
        {
            public IReadOnlyDictionary<string, string> MetadataPairs { get; set; } = new Dictionary<string, string>();

            protected override void CreateRequestHeaders(HttpRequestMessage request)
            {
                base.CreateRequestHeaders(request);
                BlobWireFormat.AddMetadata(request, MetadataPairs);
            }
        }
    }
}
