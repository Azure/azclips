// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Creates a share (<see cref="CreateShareProcessor"/>). The generated processor sends metadata as a single
    /// <c>x-ms-meta</c> header; the service expects one <c>x-ms-meta-{key}</c> header per pair, so it is added here.
    /// </summary>
    public class CreateShareProcessor_Orchestrator : ProcessorBase<object, ShareCreateResult>, ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName));

        /// <summary>Metadata as <c>key=value</c> pairs.</summary>
        public string[]? Metadata { get; set; }

        /// <summary>The share quota in GiB.</summary>
        public int? Quota { get; set; }

        public string? AccessTier { get; set; }
        public string? EnabledProtocols { get; set; }
        public string? RootSquash { get; set; }
        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                Dictionary<string, string> metadata = FileWireFormat.ParsePairs(Metadata, nameof(Metadata));
                using HttpResponseMessage response = await CreateExtendedProcessor<CreateShareRequest>(p =>
                {
                    p.Url = Endpoint;
                    p.MetadataPairs = metadata;
                    p.Quota = Quota;
                    p.AccessTier = AccessTier;
                    p.EnabledProtocols = EnabledProtocols;
                    p.RootSquash = RootSquash;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();
                await WriteOutput(ShareCreateResult.From(response));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        private sealed class CreateShareRequest : CreateShareProcessor
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
