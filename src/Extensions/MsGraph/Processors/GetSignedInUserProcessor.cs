// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Atomic processor for <c>GET /me</c>. Convenience features (merging <c>$select</c> with a
    /// default property set) live in
    /// <c>Orchestrators/GetSignedInUserProcessor_Orchestrator</c>.
    /// </summary>
    public class GetSignedInUserProcessor : GraphProcessorBase<User>
    {
        [ArgumentInfo(
            Required = false,
            Description = @"Select properties to be returned",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Expand related entities",
            SerializedName = @"Expand")]
        public string[]? Expand { get; set; }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            string uri = "https://graph.microsoft.com/v1.0/me"
                + ODataQueryBuilder.Build(select: Select, expand: Expand);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}
