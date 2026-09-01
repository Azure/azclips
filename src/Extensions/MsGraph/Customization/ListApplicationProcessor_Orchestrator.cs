// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Convenience orchestrator wrapping the atomic <see cref="ListApplicationProcessor"/>. Exposes
    /// the filter shortcut <c>DisplayName</c> that translates into an OData <c>$filter</c> clause
    /// and combines with the caller's <c>Filter</c> using OData <c>and</c>. The atomic itself
    /// accepts only native OData query params.
    /// </summary>
    public class ListApplicationProcessor_Orchestrator : GraphProcessorBase<object, ApplicationList>
    {
        [ArgumentInfo(
            Required = false,
            Description = @"OData $filter expression, for example ""signInAudience eq 'AzureADMyOrg'"". Combines with the DisplayName shortcut using OData 'and'.",
            SerializedName = @"Filter")]
        public string? Filter { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"List applications with the specified display name.",
            SerializedName = @"DisplayName")]
        public string? DisplayName { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Properties to return.",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

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

        internal override async Task<HttpResponseMessage> SendAsync()
        {
            string? displayNameClause = string.IsNullOrWhiteSpace(DisplayName)
                ? null
                : $"displayName eq '{SelectClauseHelpers.EscapeODataString(DisplayName)}'";

            string? effectiveFilter = SelectClauseHelpers.CombineFilterClauses(Filter, displayNameClause);

            ListApplicationProcessor atomic = CreateExtendedProcessor<ListApplicationProcessor>(p =>
            {
                p.Filter = effectiveFilter;
                p.Select = Select;
                p.Orderby = Orderby;
                p.Search = Search;
                p.ConsistencyLevel = ConsistencyLevel;
            });
            return await atomic.SendAsync();
        }
    }
}
