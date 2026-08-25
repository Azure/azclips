// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Atomic processor for <c>GET /applications</c>. Takes only what the Microsoft Graph endpoint
    /// accepts natively: OData <c>$filter</c>, <c>$select</c>, <c>$orderby</c>, <c>$search</c>, and
    /// the <c>ConsistencyLevel</c> header. Convenience filter shortcuts (<c>DisplayName</c>) live in
    /// <c>Orchestrators/ListApplicationProcessor_Orchestrator</c>. Returns an OData envelope; this
    /// processor writes its <c>value</c> array. Pagination (<c>@odata.nextLink</c>) is intentionally
    /// not followed yet.
    /// </summary>
    public class ListApplicationProcessor : GraphProcessorBase<ApplicationList>
    {
        [ArgumentInfo(
            Required = false,
            Description = @"OData $filter expression applied by Microsoft Graph. Passed through verbatim; Graph validates the syntax.",
            SerializedName = @"Filter")]
        public string? Filter { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Properties to project via OData $select.",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Property paths to sort by via OData $orderby (for example, displayName).",
            SerializedName = @"Orderby")]
        public string[]? Orderby { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Free-text search expression mapped to OData $search (for example, ""displayName:contoso"").",
            SerializedName = @"Search")]
        public string? Search { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Value sent as the ConsistencyLevel HTTP header. Required (typically 'eventual') when using advanced queries such as $search or $orderby on non-indexed properties.",
            SerializedName = @"ConsistencyLevel")]
        public string? ConsistencyLevel { get; set; }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            string uri = "https://graph.microsoft.com/v1.0/applications"
                + ODataQueryBuilder.Build(filter: Filter, select: Select, orderby: Orderby, search: Search);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.SetConsistencyLevel(ConsistencyLevel);

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        protected override object SelectOutput(ApplicationList body) => body.Value ?? [];
    }
}

