// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Atomic processor for <c>GET /groups/{id}</c>. Takes only what the Microsoft Graph
    /// <c>groups</c> endpoint accepts: an object ID, OData <c>$select</c>, and OData
    /// <c>$expand</c>. Convenience features that layer on top (display-name resolution,
    /// merging <c>$select</c> with a default property set, ...) live in
    /// <c>Customization/GetGroupProcessor_Orchestrator</c>.
    /// </summary>
    public class GetGroupProcessor : GraphProcessorBase<object, Group>
    {
        [ArgumentInfo(
            Required = true,
            Description = @"key: id of group",
            SerializedName = @"Id")]
        public string Id { get; set; } = "";

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

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://graph.microsoft.com/v1.0/groups/{Uri.EscapeDataString(Id)}"
                + ODataQueryBuilder.Build(select: Select, expand: Expand);
    }
}
