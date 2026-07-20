// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Graph.Models;

namespace Microsoft.Azure.Azclips.Extensions.Graph.Processors
{
    /// <summary>
    /// Retrieves the currently signed-in Microsoft Graph user via <c>GET /me</c>.
    /// </summary>
    public class GetSignedInUserProcessor : GraphProcessorBase<User>
    {
        [ArgumentInfo(
            Required = false,
            Description = @"Properties to project via OData $select. Combine with AppendSelected to widen rather than replace the default property set.",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Related navigation properties to expand via OData $expand (for example, memberOf).",
            SerializedName = @"Expand")]
        public string[]? Expand { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"When set, prepends the default Microsoft Graph user property set to the values supplied via Select instead of replacing them.",
            SerializedName = @"AppendSelected")]
        public bool AppendSelected { get; set; }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            var queryParts = new List<string>();

            string? selectClause = GraphUserDefaults.BuildSelectClause(Select, AppendSelected);
            if (selectClause is not null)
            {
                queryParts.Add($"$select={Uri.EscapeDataString(selectClause)}");
            }

            if (Expand is { Length: > 0 })
            {
                queryParts.Add($"$expand={Uri.EscapeDataString(string.Join(',', Expand))}");
            }

            string uri = $"{GraphBaseUri}/me";
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
