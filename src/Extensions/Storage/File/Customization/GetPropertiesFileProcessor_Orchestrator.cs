// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Core.Abstractions.Runtime;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;

namespace Microsoft.Azure.AzClips.Extensions.StorageFile.Processors
{
    /// <summary>
    /// Get File Properties is a HEAD answered entirely in response headers, so the generated
    /// <see cref="GetPropertiesFileProcessor"/> writes nothing. This processor maps them onto <see cref="ShareFile"/>.
    /// </summary>
    public class GetPropertiesFileProcessor_Orchestrator : ProcessorBase<object, ShareFile>, ISasAuthorized
    {
        public string? SasToken { get; set; }

        /// <summary>Share name, used with <see cref="ProcessorBase{TRequest, TResponse}.StorageAccountName"/> when <see cref="ProcessorBase.Url"/> is not set.</summary>
        public string? ShareName { get; set; }

        /// <summary>File path within the share, e.g. <c>dir/file.txt</c>.</summary>
        public string? FilePath { get; set; }

        protected override string Endpoint => ResolveUrl((nameof(ShareName), ShareName), (nameof(FilePath), FilePath?.Trim('/')));

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
                using HttpResponseMessage response = await CreateExtendedProcessor<GetPropertiesFileProcessor>(p =>
                {
                    p.Url = url;
                    p.Sharesnapshot = Snapshot;
                    p.Timeout = Timeout;
                    p.ClientRequestId = ClientRequestId;
                }).SendAsync();
                await WriteOutput(ShareFile.FromHeaders(response, url));
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}
