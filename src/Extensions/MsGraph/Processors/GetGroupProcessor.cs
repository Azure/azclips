// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Retrieves a single Microsoft Graph group via <c>GET /groups/{id}</c>. Accepts either the
    /// object ID or a display name prefix that matches exactly one group.
    /// </summary>
    public class GetGroupProcessor : GraphProcessorBase<Group>
    {
        [ArgumentInfo(
            Required = true,
            Description = @"The group's object ID or display name (prefix works if there is a unique match).",
            SerializedName = @"Group")]
        public string Group { get; set; } = "";

        [ArgumentInfo(
            Required = false,
            Description = @"Properties to return. Combine with AppendSelected to include the default properties as well.",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Related properties to include in the response, for example members.",
            SerializedName = @"Expand")]
        public string[]? Expand { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"When set with Select, include the default property set alongside the values in Select rather than replacing them.",
            SerializedName = @"AppendSelected")]
        public bool AppendSelected { get; set; }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            string groupId = await ResolveGroupIdentifierAsync(Group);

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            var queryParts = new List<string>();

            string? selectClause = GraphGroupDefaults.BuildSelectClause(Select, AppendSelected);
            if (selectClause is not null)
            {
                queryParts.Add($"$select={Uri.EscapeDataString(selectClause)}");
            }

            if (Expand is { Length: > 0 })
            {
                queryParts.Add($"$expand={Uri.EscapeDataString(string.Join(',', Expand))}");
            }

            string uri = $"{GraphBaseUri}/groups/{Uri.EscapeDataString(groupId)}";
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
