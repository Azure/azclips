// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Atomic processor for <c>GET /groups/{id}/members</c>. Takes only what the Microsoft
    /// Graph endpoint accepts: the group's object ID and OData query options. Convenience
    /// features (accepting a display-name prefix or exact name for the group, ...) live in
    /// <c>Customization/ListGroupMemberProcessor_Orchestrator</c>.
    /// </summary>
    /// <remarks>
    /// Hand-authored atomic: <c>/groups/{group-id}/members</c> is only referenced via an
    /// <c>x-ms-links</c> operationId in the azure-powershell v1.0 OpenAPI spec cut and doesn't
    /// have a full inline path definition. Once codegen either follows those link references or
    /// consumes the upstream <c>msgraph-metadata</c> spec (which does define the full sub-path),
    /// this class becomes a codegen candidate.
    /// </remarks>
    public class ListGroupMemberProcessor : GraphProcessorBase<object, DirectoryObjectList>
    {
        [ArgumentInfo(
            Required = true,
            Description = @"key: id of group",
            SerializedName = @"GroupId")]
        public string GroupId { get; set; } = "";

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

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://graph.microsoft.com/v1.0/groups/{Uri.EscapeDataString(GroupId)}/members"
                + ODataQueryBuilder.Build(filter: Filter, select: Select, expand: Expand, orderby: Orderby, search: Search);

        protected override void CreateRequestHeaders(HttpRequestMessage request) => request.SetConsistencyLevel(ConsistencyLevel);

        private string? nextLink(DirectoryObjectList body) => body.NextLink;
        private DirectoryObject[]? pageItems(DirectoryObjectList body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DirectoryObjectList? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
