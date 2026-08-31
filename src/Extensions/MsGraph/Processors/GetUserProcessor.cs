// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Atomic processor for <c>GET /users/{id}</c>. Takes only what the Microsoft Graph
    /// <c>users</c> endpoint accepts: an object ID or user principal name, OData
    /// <c>$select</c>, and OData <c>$expand</c>. Convenience features (merging
    /// <c>$select</c> with a default property set, ...) live in
    /// <c>Orchestrators/GetUserProcessor_Orchestrator</c>.
    /// </summary>
    public class GetUserProcessor : GraphProcessorBase<User>
    {
        [ArgumentInfo(
            Required = true,
            Description = @"key: id of user",
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

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new InvalidOperationException("Id is required to get a user.");
            }

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            string uri = $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(Id)}"
                + ODataQueryBuilder.Build(select: Select, expand: Expand);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}
