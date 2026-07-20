// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Graph.Models;

namespace Microsoft.Azure.Azclips.Extensions.Graph.Processors
{
    /// <summary>
    /// Lists Microsoft Graph application registrations. GET <c>/applications</c> returns an envelope
    /// <c>{ "value": [...], "@odata.nextLink": "..." }</c>; this processor writes its <c>value</c>
    /// array. Pagination (<c>@odata.nextLink</c>) is intentionally not followed yet.
    /// </summary>
    public class ListApplicationProcessor : GraphProcessorBase<ApplicationList>
    {
        private const string ConsistencyLevelHeaderName = "ConsistencyLevel";

        [ArgumentInfo(
            Required = false,
            Description = @"OData $filter expression applied by Microsoft Graph. Passed through verbatim; Graph validates the syntax. Combines with the DisplayName shortcut using OData 'and'.",
            SerializedName = @"Filter")]
        public string? Filter { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Exact display name to match. Translated to the OData filter displayName eq 'value'.",
            SerializedName = @"DisplayName")]
        public string? DisplayName { get; set; }

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
            Description = @"Free-text search expression mapped to OData $search (for example, \""displayName:contoso\"").",
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

            var queryParts = new List<string>();

            string? effectiveFilter = BuildFilterClause();
            if (effectiveFilter is not null)
            {
                queryParts.Add($"$filter={Uri.EscapeDataString(effectiveFilter)}");
            }

            if (Select is { Length: > 0 })
            {
                queryParts.Add($"$select={Uri.EscapeDataString(string.Join(',', Select))}");
            }

            if (Orderby is { Length: > 0 })
            {
                queryParts.Add($"$orderby={Uri.EscapeDataString(string.Join(',', Orderby))}");
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                queryParts.Add($"$search={Uri.EscapeDataString(Search)}");
            }

            string uri = $"{GraphBaseUri}/applications";
            if (queryParts.Count > 0)
            {
                uri = $"{uri}?{string.Join('&', queryParts)}";
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);

            if (!string.IsNullOrWhiteSpace(ConsistencyLevel))
            {
                // Graph-specific header; TryAddWithoutValidation bypasses .NET's built-in allow-list.
                request.Headers.TryAddWithoutValidation(ConsistencyLevelHeaderName, ConsistencyLevel);
            }

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        // Combines the raw Filter with the DisplayName shortcut via OData 'and'.
        // Single quotes inside shortcut values are doubled per OData string-literal rules.
        private string? BuildFilterClause()
        {
            var clauses = new List<string>();

            if (!string.IsNullOrWhiteSpace(Filter))
            {
                clauses.Add(Filter);
            }
            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                clauses.Add($"displayName eq '{EscapeODataString(DisplayName)}'");
            }

            return clauses.Count == 0 ? null : string.Join(" and ", clauses);
        }

        private static string EscapeODataString(string value) => value.Replace("'", "''");

        protected override object SelectOutput(ApplicationList body) => body.Value ?? [];
    }
}
