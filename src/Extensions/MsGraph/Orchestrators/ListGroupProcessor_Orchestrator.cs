// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Convenience orchestrator wrapping the atomic <see cref="ListGroupProcessor"/>. Exposes
    /// filter shortcuts (<c>DisplayName</c>, <c>DisplayNamePrefix</c>) that translate into an
    /// OData <c>$filter</c>, and the <c>AppendSelected</c> switch that merges the caller's
    /// <c>Select</c> with a default group property set. The atomic itself accepts only native
    /// OData query params.
    /// </summary>
    public class ListGroupProcessor_Orchestrator : GraphProcessorBase<GroupList>
    {
        [ArgumentInfo(
            Required = false,
            Description = @"OData $filter expression, for example ""securityEnabled eq true"". Combines with the convenience shortcuts (DisplayName, DisplayNamePrefix) using OData 'and'.",
            SerializedName = @"Filter")]
        public string? Filter { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"List groups with the specified display name.",
            SerializedName = @"DisplayName")]
        public string? DisplayName { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"List groups whose display name starts with the specified prefix.",
            SerializedName = @"DisplayNamePrefix")]
        public string? DisplayNamePrefix { get; set; }

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
            Description = @"OData $orderby values, for example 'displayName' or 'createdDateTime desc'.",
            SerializedName = @"Orderby")]
        public string[]? Orderby { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"OData $search expression, for example ""displayName:contoso"". Requires ConsistencyLevel eventual.",
            SerializedName = @"Search")]
        public string? Search { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Set to 'eventual' to enable advanced query features such as $search, $count, or $orderby on non-indexed properties.",
            SerializedName = @"ConsistencyLevel")]
        public string? ConsistencyLevel { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"When set with Select, include the default property set alongside the values in Select rather than replacing them.",
            SerializedName = @"AppendSelected")]
        public bool AppendSelected { get; set; }

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            EnsureRuntimeContext();

            string? displayNameClause = string.IsNullOrWhiteSpace(DisplayName)
                ? null
                : $"displayName eq '{SelectClauseHelpers.EscapeODataString(DisplayName)}'";
            string? prefixClause = string.IsNullOrWhiteSpace(DisplayNamePrefix)
                ? null
                : $"startswith(displayName,'{SelectClauseHelpers.EscapeODataString(DisplayNamePrefix)}')";

            string? effectiveFilter = SelectClauseHelpers.CombineFilterClauses(
                Filter, displayNameClause, prefixClause);

            string[]? mergedSelect = SelectClauseHelpers.MergeGroupSelect(Select, AppendSelected);

            ListGroupProcessor atomic = CreateExtendedProcessor<ListGroupProcessor>(p =>
            {
                p.Filter = effectiveFilter;
                p.Select = mergedSelect;
                p.Expand = Expand;
                p.Orderby = Orderby;
                p.Search = Search;
                p.ConsistencyLevel = ConsistencyLevel;
            });
            return await atomic.SendAsync();
        }

        protected override object SelectOutput(GroupList body) => body.Value ?? [];
    }
}
