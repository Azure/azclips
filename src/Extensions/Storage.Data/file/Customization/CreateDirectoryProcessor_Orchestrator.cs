// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Creates a directory (<see cref="CreateDirectoryProcessor"/>); its parent must already exist. Metadata is sent as
    /// one <c>x-ms-meta-{key}</c> header per pair, and the response headers are written as a <see cref="DirectoryEntry"/>.
    /// </summary>
    public class CreateDirectoryProcessor_Orchestrator : ProcessorBase<object, DirectoryEntry>, ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        /// <summary>Directory path within the share, e.g. <c>parent/child</c>.</summary>
        public string? DirectoryPath { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName), (nameof(DirectoryPath), DirectoryPath?.Trim('/')));

        /// <summary>Metadata as <c>key=value</c> pairs.</summary>
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
                string url = Endpoint;
                using HttpResponseMessage response = await CreateExtendedProcessor<CreateDirectoryRequest>(p =>
                {
                    p.Url = url;
                    p.MetadataPairs = metadata;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();
                await WriteOutput(DirectoryEntry.FromHeaders(response, url));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        private sealed class CreateDirectoryRequest : CreateDirectoryProcessor
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
