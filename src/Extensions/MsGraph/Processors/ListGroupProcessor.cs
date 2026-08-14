// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Atomic processor for <c>GET /groups</c>. Takes only what the Microsoft Graph endpoint
    /// accepts natively: OData <c>$filter</c>, <c>$select</c>, <c>$expand</c>,
    /// <c>$orderby</c>, <c>$search</c>, and the <c>ConsistencyLevel</c> header. Convenience
    /// filter shortcuts (<c>DisplayName</c>, <c>DisplayNamePrefix</c>) and
    /// <c>AppendSelected</c> live in <c>Orchestrators/ListGroupProcessor_Orchestrator</c>.
    /// Returns an OData envelope; this processor writes its <c>value</c> array. Pagination
    /// (<c>@odata.nextLink</c>) is not followed.
    /// </summary>
    public class ListGroupProcessor : GraphProcessorBase<GroupList>
    {
        private const string ConsistencyLevelHeaderName = "ConsistencyLevel";

        [ArgumentInfo(
            Required = false,
            Description = @"OData $filter expression, for example ""securityEnabled eq true"".",
            SerializedName = @"Filter")]
        public string? Filter { get; set; }

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

        [ArgumentInfo(
            Required = false,
            Description = @"OData $orderby values, for example 'displayName' or 'createdDateTime desc'.",
            SerializedName = @"Orderby")]
        public string[]? Orderby { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"OData $search expression, for example \""displayName:contoso\"". Requires ConsistencyLevel eventual.",
            SerializedName = @"Search")]
        public string? Search { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Set to 'eventual' to enable advanced query features such as $search, $count, or $orderby on non-indexed properties.",
            SerializedName = @"ConsistencyLevel")]
        public string? ConsistencyLevel { get; set; }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            var queryParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Filter))
            {
                queryParts.Add($"$filter={Uri.EscapeDataString(Filter)}");
            }

            if (Select is { Length: > 0 })
            {
                queryParts.Add($"$select={Uri.EscapeDataString(string.Join(',', Select))}");
            }

            if (Expand is { Length: > 0 })
            {
                queryParts.Add($"$expand={Uri.EscapeDataString(string.Join(',', Expand))}");
            }

            if (Orderby is { Length: > 0 })
            {
                queryParts.Add($"$orderby={Uri.EscapeDataString(string.Join(',', Orderby))}");
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                queryParts.Add($"$search={Uri.EscapeDataString(Search)}");
            }

            string uri = $"{GraphBaseUri}/groups";
            if (queryParts.Count > 0)
            {
                uri = $"{uri}?{string.Join('&', queryParts)}";
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);

            if (!string.IsNullOrWhiteSpace(ConsistencyLevel))
            {
                request.Headers.TryAddWithoutValidation(ConsistencyLevelHeaderName, ConsistencyLevel);
            }

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        protected override object SelectOutput(GroupList body) => body.Value ?? [];
    }
}
