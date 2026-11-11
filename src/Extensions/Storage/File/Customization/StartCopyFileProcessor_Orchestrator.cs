// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Copy File on top of the generated <see cref="StartCopyFileProcessor"/>: metadata is sent as one
    /// <c>x-ms-meta-{key}</c> header per pair and <c>x-ms-copy-source</c> is URL-encoded. The response is headers only
    /// and is mapped to <see cref="FileCopyResult"/>.
    /// </summary>
    /// <remarks>
    /// The source is read by the service, not signed by this request: it must be in the destination account or carry a SAS
    /// in <see cref="SourceUrl"/>. It can be a file or a blob.
    /// </remarks>
    public class StartCopyFileProcessor_Orchestrator : ProcessorBase<object, FileCopyResult>, ISasAuthorized
    {
        /// <summary>SAS for the destination; a SAS for the source belongs in <see cref="SourceUrl"/>.</summary>
        public string? SasToken { get; set; }

        /// <summary>The source file or blob URL, unencoded or encoded; a SAS query string is kept as given.</summary>
        public string SourceUrl { get; set; } = string.Empty;

        /// <summary>Destination share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        /// <summary>Destination file path within the share, e.g. <c>dir/file.txt</c>.</summary>
        public string? FilePath { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName), (nameof(FilePath), FilePath?.Trim('/')));

        /// <summary>Destination metadata as <c>key=value</c> pairs; when not given, the source metadata is copied.</summary>
        public string[]? Metadata { get; set; }

        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                Dictionary<string, string> metadata = FileWireFormat.ParsePairs(Metadata, nameof(Metadata));
                using HttpResponseMessage response = await CreateExtendedProcessor<StartCopyRequest>(p =>
                {
                    p.Url = Endpoint;
                    p.CopySource = FileWireFormat.EncodeUrl(SourceUrl, nameof(SourceUrl));
                    p.MetadataPairs = metadata;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();
                await WriteOutput(FileCopyResult.From(response));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        private sealed class StartCopyRequest : StartCopyFileProcessor
        {
            public IReadOnlyDictionary<string, string> MetadataPairs { get; set; } = new Dictionary<string, string>();

            protected override void CreateRequestHeaders(HttpRequestMessage request)
            {
                base.CreateRequestHeaders(request);
                FileWireFormat.AddMetadata(request, MetadataPairs);
            }
        }
    }
}
