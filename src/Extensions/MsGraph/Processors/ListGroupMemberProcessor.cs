// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Atomic processor for <c>GET /groups/{id}/members</c>. Takes only what the Microsoft
    /// Graph endpoint accepts: the group's object ID and OData query options. Convenience
    /// features (accepting a display-name prefix or exact name for the group, ...) live in
    /// <c>Orchestrators/ListGroupMemberProcessor_Orchestrator</c>.
    /// </summary>
    public class ListGroupMemberProcessor : GraphProcessorBase<DirectoryObjectList>
    {
        private const string ConsistencyLevelHeaderName = "ConsistencyLevel";

        [ArgumentInfo(
            Required = true,
            Description = @"The object ID of the group whose members to list.",
            SerializedName = @"GroupId")]
        public string GroupId { get; set; } = "";

        [ArgumentInfo(
            Required = false,
            Description = @"OData $filter expression.",
            SerializedName = @"Filter")]
        public string? Filter { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Properties to return via OData $select.",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Related properties to include in the response via OData $expand.",
            SerializedName = @"Expand")]
        public string[]? Expand { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"OData $orderby values.",
            SerializedName = @"Orderby")]
        public string[]? Orderby { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"OData $search expression. Requires ConsistencyLevel eventual.",
            SerializedName = @"Search")]
        public string? Search { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Set to 'eventual' to enable advanced query features such as $search, $count, or $orderby on non-indexed properties.",
            SerializedName = @"ConsistencyLevel")]
        public string? ConsistencyLevel { get; set; }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            if (string.IsNullOrWhiteSpace(GroupId))
            {
                throw new InvalidOperationException("GroupId is required to list group members.");
            }

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            string uri = $"https://graph.microsoft.com/v1.0/groups/{Uri.EscapeDataString(GroupId)}/members"
                + ODataQueryBuilder.Build(filter: Filter, select: Select, expand: Expand, orderby: Orderby, search: Search);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);

            if (!string.IsNullOrWhiteSpace(ConsistencyLevel))
            {
                request.Headers.TryAddWithoutValidation(ConsistencyLevelHeaderName, ConsistencyLevel);
            }

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        protected override object SelectOutput(DirectoryObjectList body) => body.Value ?? [];
    }
}
