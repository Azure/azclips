// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Graph.Models;

namespace Microsoft.Azure.Azclips.Extensions.Graph.Processors
{
    /// <summary>
    /// Lists Microsoft Graph users. GET <c>/users</c> returns an envelope
    /// <c>{ "value": [...], "@odata.nextLink": "..." }</c>; this processor writes its
    /// <c>value</c> array. Pagination (<c>@odata.nextLink</c>) is intentionally not followed yet.
    /// </summary>
    public class ListUserProcessor : GraphProcessorBase<UserList>
    {
        private const string ConsistencyLevelHeaderName = "ConsistencyLevel";

        [ArgumentInfo(
            Required = false,
            Description = @"OData $filter expression applied by Microsoft Graph. Passed through verbatim; Graph validates the syntax. Combines with the convenience shortcuts (DisplayName, Mail, StartsWith, UserPrincipalName) using OData 'and'.",
            SerializedName = @"Filter")]
        public string? Filter { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Exact display name to match. Translated to the OData filter displayName eq 'value'.",
            SerializedName = @"DisplayName")]
        public string? DisplayName { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Exact mail address to match. Translated to the OData filter mail eq 'value'.",
            SerializedName = @"Mail")]
        public string? Mail { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Prefix to match against the display name. Translated to the OData filter startswith(displayName,'value').",
            SerializedName = @"StartsWith")]
        public string? StartsWith { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Exact user principal name to match. Translated to the OData filter userPrincipalName eq 'value'.",
            SerializedName = @"UserPrincipalName")]
        public string? UserPrincipalName { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Properties to project via OData $select. Combine with AppendSelected to widen rather than replace the default property set.",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Related navigation properties to expand via OData $expand (for example, memberOf).",
            SerializedName = @"Expand")]
        public string[]? Expand { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Property paths to sort by via OData $orderby (for example, displayName, mail desc).",
            SerializedName = @"Orderby")]
        public string[]? Orderby { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Free-text search expression mapped to OData $search (for example, \""displayName:ada\"").",
            SerializedName = @"Search")]
        public string? Search { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Value sent as the ConsistencyLevel HTTP header. Required (typically 'eventual') when using advanced queries such as $search, $count, or $orderby on non-indexed properties.",
            SerializedName = @"ConsistencyLevel")]
        public string? ConsistencyLevel { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"When set, prepends the default Microsoft Graph user property set to the values supplied via Select instead of replacing them.",
            SerializedName = @"AppendSelected")]
        public bool AppendSelected { get; set; }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            var queryParts = new List<string>();

            string? effectiveFilter = BuildFilterClause();
            if (effectiveFilter is not null)
            {
                queryParts.Add($"$filter={Uri.EscapeDataString(effectiveFilter)}");
            }

            string? selectClause = GraphUserDefaults.BuildSelectClause(Select, AppendSelected);
            if (selectClause is not null)
            {
                queryParts.Add($"$select={Uri.EscapeDataString(selectClause)}");
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

            string uri = $"{GraphBaseUri}/users";
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

        // Combines the raw Filter with any set convenience shortcuts via OData 'and'.
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
            if (!string.IsNullOrWhiteSpace(Mail))
            {
                clauses.Add($"mail eq '{EscapeODataString(Mail)}'");
            }
            if (!string.IsNullOrWhiteSpace(StartsWith))
            {
                clauses.Add($"startswith(displayName,'{EscapeODataString(StartsWith)}')");
            }
            if (!string.IsNullOrWhiteSpace(UserPrincipalName))
            {
                clauses.Add($"userPrincipalName eq '{EscapeODataString(UserPrincipalName)}'");
            }

            return clauses.Count == 0 ? null : string.Join(" and ", clauses);
        }

        private static string EscapeODataString(string value) => value.Replace("'", "''");

        protected override object SelectOutput(UserList body) => body.Value ?? [];
    }
}
