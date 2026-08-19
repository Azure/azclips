// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Convenience orchestrator wrapping the atomic <see cref="ListGroupMemberProcessor"/>. Accepts
    /// a group object ID, display-name prefix, or exact display name via
    /// <see cref="GroupDisplayName"/>, resolves that to a single object ID, then delegates to the
    /// atomic. The atomic itself only accepts an object ID.
    /// </summary>
    public class ListGroupMemberProcessor_Orchestrator : GraphProcessorBase<DirectoryObjectList>
    {
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

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            EnsureRuntimeContext();
            string groupId = await ResolveGroupAsync();

            ListGroupMemberProcessor atomic = CreateExtendedProcessor<ListGroupMemberProcessor>(p =>
            {
                p.GroupId = groupId;
                p.Filter = Filter;
                p.Select = Select;
                p.Expand = Expand;
                p.Orderby = Orderby;
                p.Search = Search;
                p.ConsistencyLevel = ConsistencyLevel;
            });
            return await atomic.SendAsync();
        }

        private async Task<string> ResolveGroupAsync()
        {
            if (!string.IsNullOrWhiteSpace(GroupDisplayName))
            {
                return await ResolveByExactDisplayNameAsync(GroupDisplayName);
            }
            if (!string.IsNullOrWhiteSpace(Group))
            {
                return await ResolveByIdentifierAsync(Group);
            }
            throw new InvalidOperationException("A group identifier is required. Provide Group or GroupDisplayName.");
        }

        private async Task<string> ResolveByIdentifierAsync(string identifier)
        {
            if (Guid.TryParse(identifier, out _))
            {
                return identifier;
            }

            ListGroupProcessor list = CreateExtendedProcessor<ListGroupProcessor>(p =>
            {
                p.Filter = $"startswith(displayName,'{SelectClauseHelpers.EscapeODataString(identifier)}')";
                p.Select = ["id", "displayName"];
            });
            return await GroupResolver.PickSingleGroupIdAsync(list, JsonOptions, identifier);
        }

        private async Task<string> ResolveByExactDisplayNameAsync(string displayName)
        {
            ListGroupProcessor list = CreateExtendedProcessor<ListGroupProcessor>(p =>
            {
                p.Filter = $"displayName eq '{SelectClauseHelpers.EscapeODataString(displayName)}'";
                p.Select = ["id", "displayName"];
            });
            return await GroupResolver.PickSingleGroupIdAsync(list, JsonOptions, displayName);
        }

        protected override object SelectOutput(DirectoryObjectList body) => body.Value ?? [];
    }
}
