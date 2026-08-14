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
            Description = @"The object ID of the group.",
            SerializedName = @"Id")]
        public string Id { get; set; } = "";

        [ArgumentInfo(
            Required = false,
            Description = @"Properties to return via OData $select.",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Related properties to include in the response via OData $expand, for example members.",
            SerializedName = @"Expand")]
        public string[]? Expand { get; set; }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new InvalidOperationException("Id is required to get a group.");
            }

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            var queryParts = new List<string>();
            if (Select is { Length: > 0 })
            {
                queryParts.Add($"$select={Uri.EscapeDataString(string.Join(',', Select))}");
            }
            if (Expand is { Length: > 0 })
            {
                queryParts.Add($"$expand={Uri.EscapeDataString(string.Join(',', Expand))}");
            }

            string uri = $"{GraphBaseUri}/groups/{Uri.EscapeDataString(Id)}";
            if (queryParts.Count > 0)
            {
                uri = $"{uri}?{string.Join('&', queryParts)}";
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}
