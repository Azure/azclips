// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Get Directory Properties answers entirely in response headers, so the generated
    /// <see cref="GetPropertiesDirectoryProcessor"/> writes nothing. This processor maps them onto
    /// <see cref="ShareDirectory"/>.
    /// </summary>
    public class GetPropertiesDirectoryProcessor_Orchestrator : ProcessorBase<object, ShareDirectory>, ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        /// <summary>Directory path within the share, e.g. <c>parent/child</c>.</summary>
        public string? DirectoryPath { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName), (nameof(DirectoryPath), DirectoryPath?.Trim('/')));

        public string? Snapshot { get; set; }
        public int? Timeout { get; set; }
        public string? ClientRequestId { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                string url = Endpoint;
                using HttpResponseMessage response = await CreateExtendedProcessor<GetPropertiesDirectoryProcessor>(p =>
                {
                    p.Url = url;
                    p.Sharesnapshot = Snapshot;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();
                await WriteOutput(ShareDirectory.FromHeaders(response, url));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}
