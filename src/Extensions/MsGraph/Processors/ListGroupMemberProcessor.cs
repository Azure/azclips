// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Graph.Models;

namespace Microsoft.Azure.Azclips.Extensions.Graph.Processors
{
    /// <summary>
    /// Lists members of a Microsoft Graph group via <c>GET /groups/{id}/members</c>. The group
    /// can be identified by object ID, display name prefix (single match required), or exact
    /// display name via <see cref="GroupDisplayName"/>.
    /// </summary>
    public class ListGroupMemberProcessor : GraphProcessorBase<DirectoryObjectList>
    {
        private const string ConsistencyLevelHeaderName = "ConsistencyLevel";

        [ArgumentInfo(
            Required = false,
            Description = @"The group's object ID or display name (prefix works if there is a unique match).",
            SerializedName = @"Group")]
        public string? Group { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"The exact display name of the target group.",
            SerializedName = @"GroupDisplayName")]
        public string? GroupDisplayName { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"OData $filter expression.",
            SerializedName = @"Filter")]
        public string? Filter { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Properties to return.",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Related properties to include in the response.",
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
            string groupId = await ResolveGroupAsync();

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

            string uri = $"{GraphBaseUri}/groups/{Uri.EscapeDataString(groupId)}/members";
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

        private async Task<string> ResolveGroupAsync()
        {
            if (!string.IsNullOrWhiteSpace(GroupDisplayName))
            {
                return await ResolveGroupByExactDisplayNameAsync(GroupDisplayName);
            }
            if (!string.IsNullOrWhiteSpace(Group))
            {
                return await ResolveGroupIdentifierAsync(Group);
            }
            throw new InvalidOperationException("A group identifier is required. Provide Group or GroupDisplayName.");
        }

        protected override object SelectOutput(DirectoryObjectList body) => body.Value ?? [];
    }
}
