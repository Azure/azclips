// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageBlob.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Creates a block blob from a source URL in one synchronous Put Blob From URL
    /// (<see cref="UploadBlobFromUrlBlockBlobProcessor"/>; source up to 5000 MiB). An existing blob is never
    /// overwritten (<c>If-None-Match: *</c>).
    /// </summary>
    /// <remarks>
    /// As with Start Copy, the source must be public, in the destination account, or carry a SAS in <see cref="SourceUrl"/>.
    /// </remarks>
    public class UploadBlobFromUrlBlockBlobProcessor_Orchestrator : ProcessorBase<object, BlobUploadResult>, ISasAuthorized
    {
        private const string BlockBlob = "BlockBlob";
        private const string NoOverwrite = "*";

        /// <summary>SAS for the destination; a SAS for the source belongs in <see cref="SourceUrl"/>.</summary>
        public string? SasToken { get; set; }

        /// <summary>The source blob URL, unencoded or encoded; a SAS query string is kept as given.</summary>
        public string SourceUrl { get; set; } = string.Empty;

        /// <summary>Destination container name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ContainerName { get; set; }

        /// <summary>Destination blob name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? BlobName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ContainerName), ContainerName), (nameof(BlobName), BlobName));

        /// <summary>Metadata as <c>key=value</c> pairs.</summary>
        public string[]? Metadata { get; set; }

        /// <summary>Tags as <c>key=value</c> pairs.</summary>
        public string[]? Tags { get; set; }

        public string? BlobContentType { get; set; }
        public string? BlobContentEncoding { get; set; }
        public string? BlobContentLanguage { get; set; }
        public string? BlobContentDisposition { get; set; }
        public string? BlobCacheControl { get; set; }
        public string? Tier { get; set; }
        public string? EncryptionScope { get; set; }

        /// <summary>Whether the source blob's properties are copied. The service default is true.</summary>
        public bool? CopySourceBlobProperties { get; set; }

        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                Dictionary<string, string> metadata = BlobWireFormat.ParsePairs(Metadata, nameof(Metadata));
                Dictionary<string, string> tags = BlobWireFormat.ParsePairs(Tags, nameof(Tags));
                using HttpResponseMessage response = await CreateExtendedProcessor<PutBlobFromUrlRequest>(p =>
                {
                    p.Url = Endpoint;
                    p.BlobType = BlockBlob;
                    p.CopySource = BlobWireFormat.EncodeUrl(SourceUrl, nameof(SourceUrl));
                    p.IfNoneMatch = NoOverwrite;
                    p.MetadataPairs = metadata;
                    p.BlobTagsString = BlobWireFormat.FormatTags(tags);
                    p.BlobContentType = BlobContentType;
                    p.BlobContentEncoding = BlobContentEncoding;
                    p.BlobContentLanguage = BlobContentLanguage;
                    p.BlobContentDisposition = BlobContentDisposition;
                    p.BlobCacheControl = BlobCacheControl;
                    p.Tier = Tier;
                    p.EncryptionScope = EncryptionScope;
                    p.CopySourceBlobProperties = CopySourceBlobProperties;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();
                await WriteOutput(BlobUploadResult.From(response));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        private sealed class PutBlobFromUrlRequest : UploadBlobFromUrlBlockBlobProcessor
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