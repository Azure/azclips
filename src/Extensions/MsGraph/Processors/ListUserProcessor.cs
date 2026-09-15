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
            Description = @"Filter items by property values",
            SerializedName = @"Filter")]
        public string? Filter { get; set; }

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

        [ArgumentInfo(
            Required = false,
            Description = @"Order items by property values",
            SerializedName = @"Orderby")]
        public string[]? Orderby { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Search items by search phrases",
            SerializedName = @"Search")]
        public string? Search { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Indicates the requested consistency level. Documentation URL: https://developer.microsoft.com/en-us/office/blogs/microsoft-graph-advanced-queries-for-directory-objects-are-now-generally-available/",
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
    }
}
