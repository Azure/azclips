// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Atomic processor for <c>GET /users</c>. Takes only what the Microsoft Graph endpoint
    /// accepts natively: OData <c>$filter</c>, <c>$select</c>, <c>$expand</c>,
    /// <c>$orderby</c>, <c>$search</c>, and the <c>ConsistencyLevel</c> header. Convenience
    /// filter shortcuts (<c>DisplayName</c>, <c>Mail</c>, <c>DisplayNamePrefix</c>,
    /// <c>UserPrincipalName</c>) and <c>AppendSelected</c> live in
    /// <c>Orchestrators/ListUserProcessor_Orchestrator</c>. Returns an OData envelope; this
    /// processor writes its <c>value</c> array. Pagination (<c>@odata.nextLink</c>) is not
    /// followed.
    /// </summary>
    public class ListUserProcessor : GraphProcessorBase<UserList>
    {
        [ArgumentInfo(
            Required = false,
            Description = @"OData $filter expression, for example ""accountEnabled eq true"".",
            SerializedName = @"Filter")]
        public string? Filter { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Properties to return via OData $select.",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Related properties to include in the response via OData $expand, for example memberOf.",
            SerializedName = @"Expand")]
        public string[]? Expand { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"OData $orderby values, for example 'displayName' or 'mail desc'.",
            SerializedName = @"Orderby")]
        public string[]? Orderby { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"OData $search expression, for example ""displayName:ada"". Requires ConsistencyLevel eventual.",
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

            string uri = "https://graph.microsoft.com/v1.0/users"
                + ODataQueryBuilder.Build(filter: Filter, select: Select, expand: Expand, orderby: Orderby, search: Search);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.SetConsistencyLevel(ConsistencyLevel);

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        protected override object SelectOutput(UserList body) => body.Value ?? [];
    }
}
