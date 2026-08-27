// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Atomic processor for <c>GET /groups/{id}</c>. Takes only what the Microsoft Graph
    /// <c>groups</c> endpoint accepts: an object ID, OData <c>$select</c>, and OData
    /// <c>$expand</c>. Convenience features that layer on top (display-name resolution,
    /// merging <c>$select</c> with a default property set, ...) live in
    /// <c>Orchestrators/GetGroupProcessor_Orchestrator</c>.
    /// </summary>
    public class GetGroupProcessor : GraphProcessorBase<Group>
    {
        [ArgumentInfo(
            Required = true,
            Description = @"key: id of group",
            SerializedName = @"Id")]
        public string Id { get; set; } = "";

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
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new InvalidOperationException("Id is required to get a group.");
            }

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            string uri = $"https://graph.microsoft.com/v1.0/groups/{Uri.EscapeDataString(Id)}"
                + ODataQueryBuilder.Build(select: Select, expand: Expand);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}
