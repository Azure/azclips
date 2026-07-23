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
            Description = @"OData $filter expression, for example ""accountEnabled eq true"". Combines with the convenience shortcuts (DisplayName, Mail, DisplayNamePrefix, UserPrincipalName) using OData 'and'.",
            SerializedName = @"Filter")]
        public string? Filter { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"List users with the specified display name.",
            SerializedName = @"DisplayName")]
        public string? DisplayName { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"List users with the specified mail address.",
            SerializedName = @"Mail")]
        public string? Mail { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"List users whose display name starts with the specified prefix.",
            SerializedName = @"DisplayNamePrefix")]
        public string? DisplayNamePrefix { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"List users with the specified user principal name.",
            SerializedName = @"UserPrincipalName")]
        public string? UserPrincipalName { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Properties to return. Combine with AppendSelected to include the default properties as well.",
            SerializedName = @"Select")]
        public string[]? Select { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"Related properties to include in the response, for example memberOf.",
            SerializedName = @"Expand")]
        public string[]? Expand { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"OData $orderby values, for example 'displayName' or 'mail desc'.",
            SerializedName = @"Orderby")]
        public string[]? Orderby { get; set; }

        [ArgumentInfo(
            Required = false,
            Description = @"OData $search expression, for example \""displayName:ada\"". Requires ConsistencyLevel eventual.",
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
                request.Headers.TryAddWithoutValidation(ConsistencyLevelHeaderName, ConsistencyLevel);
            }

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

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
            if (!string.IsNullOrWhiteSpace(DisplayNamePrefix))
            {
                clauses.Add($"startswith(displayName,'{EscapeODataString(DisplayNamePrefix)}')");
            }
            if (!string.IsNullOrWhiteSpace(UserPrincipalName))
            {
                clauses.Add($"userPrincipalName eq '{EscapeODataString(UserPrincipalName)}'");
            }

            return clauses.Count == 0 ? null : string.Join(" and ", clauses);
        }

        // OData string literal escape: single quotes are doubled.
        private static string EscapeODataString(string value) => value.Replace("'", "''");

        protected override object SelectOutput(UserList body) => body.Value ?? [];
    }
}
