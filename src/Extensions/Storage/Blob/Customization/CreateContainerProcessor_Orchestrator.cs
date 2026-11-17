// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageBlob.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageBlob.Processors
{
    /// <summary>
    /// Creates a container (<see cref="CreateContainerProcessor"/>). The generated processor sends metadata as a single
    /// <c>x-ms-meta</c> header; the service expects one <c>x-ms-meta-{key}</c> header per pair, so it is added here.
    /// </summary>
    public class CreateContainerProcessor_Orchestrator : ProcessorBase<object, ContainerCreateResult>, ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Container name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ContainerName { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ContainerName), ContainerName));

        /// <summary>Metadata as <c>key=value</c> pairs.</summary>
        public string[]? Metadata { get; set; }

        /// <summary>Public access level: <c>blob</c> or <c>container</c>. Omit for private.</summary>
        public string? PublicAccess { get; set; }

        public string? DefaultEncryptionScope { get; set; }
        public bool? PreventEncryptionScopeOverride { get; set; }
        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                Dictionary<string, string> metadata = BlobWireFormat.ParsePairs(Metadata, nameof(Metadata));
                using HttpResponseMessage response = await CreateExtendedProcessor<CreateContainerRequest>(p =>
                {
                    p.Url = Endpoint;
                    p.MetadataPairs = metadata;
                    p.Access = PublicAccess;
                    p.DefaultEncryptionScope = DefaultEncryptionScope;
                    p.PreventEncryptionScopeOverride = PreventEncryptionScopeOverride;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();
                await WriteOutput(ContainerCreateResult.From(response));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        private sealed class CreateContainerRequest : CreateContainerProcessor
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
