// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Atomic processor for <c>GET /applications</c>. Takes only what the Microsoft Graph endpoint
    /// accepts natively: OData <c>$filter</c>, <c>$select</c>, <c>$orderby</c>, <c>$search</c>, and
    /// the <c>ConsistencyLevel</c> header. Convenience filter shortcuts (<c>DisplayName</c>) live in
    /// <c>Customization/ListApplicationProcessor_Orchestrator</c>. Returns an OData envelope; this
    /// processor writes its <c>value</c> array and follows <c>@odata.nextLink</c> to return every page.
    /// </summary>
    public class ListApplicationProcessor : GraphProcessorBase<object, ApplicationList>
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

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            "https://graph.microsoft.com/v1.0/applications"
                + ODataQueryBuilder.Build(filter: Filter, select: Select, orderby: Orderby, search: Search);

        protected override void CreateRequestHeaders(HttpRequestMessage request) => request.SetConsistencyLevel(ConsistencyLevel);

        private string? nextLink(ApplicationList body) => body.NextLink;
        private Application[]? pageItems(ApplicationList body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(ApplicationList? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}

