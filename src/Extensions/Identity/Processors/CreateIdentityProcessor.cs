// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Identity.Models;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Processors
{
    public class CreateIdentityProcessor : IdentityProcessorBase<UserAssignedIdentity>
    {
        [ArgumentInfo(
        Required = true,
        Description = @"Name of resource group.",
        SerializedName = @"ResourceGroup")]
        public string ResourceGroup { get; set; } = "";

        [ArgumentInfo(
        Required = true,
        Description = @"Name of the user assigned identity.",
        SerializedName = @"ResourceName")]
        public string ResourceName { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Location.",
        SerializedName = @"Location")]
        public string Location { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Resource tags. Format: key1=value1 key2=value2",
        SerializedName = @"Tags")]
        public string[]? Tags { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Enum to configure regional restrictions on identity assignment, as necessary.",
        SerializedName = @"IsolationScope")]
        public string? IsolationScope { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Restrictions on which resource providers this identity can be assigned to.",
        SerializedName = @"ResourceRestriction")]
        public string[]? ResourceRestriction { get; set; }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            if (string.IsNullOrWhiteSpace(ResourceGroup))
                throw new InvalidOperationException("--resource-group is required.");
            if (string.IsNullOrWhiteSpace(ResourceName))
                throw new InvalidOperationException("--name or --resource-name is required.");
            if (string.IsNullOrWhiteSpace(Location))
                throw new InvalidOperationException("Location is required. Specify --location, or ensure the resource group exists so its location can be resolved automatically.");

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            string uri = BuildIdentityUri(ResourceGroup, ResourceName);

            var properties = new JsonObject();
            JsonObject? restriction = ParseAssignmentRestrictions(ResourceRestriction);
            if (restriction is not null)
            {
                properties["assignmentRestrictions"] = restriction;
            }
            if (!string.IsNullOrWhiteSpace(IsolationScope))
            {
                properties["isolationScope"] = IsolationScope;
            }

            HttpRequestMessage request = new(HttpMethod.Put, uri)
            {
                Content = JsonContent.Create(new UserAssignedIdentity
                {
                    Location = Location,
                    Tags = ParseTags(Tags),
                    Properties = properties.Count == 0
                        ? null
                        : new UserAssignedIdentityProperties
                        {
                            AssignmentRestrictions = restriction?.Deserialize<AssignmentRestrictions>(JsonOptions),
                            IsolationScope = IsolationScope
                        }
                }, options: JsonOptions)
            };

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}
