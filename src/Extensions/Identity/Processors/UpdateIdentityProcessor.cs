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
    public class UpdateIdentityProcessor : IdentityProcessorBase<UserAssignedIdentity>
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Name of resource group.",
        SerializedName = @"ResourceGroup")]
        public string ResourceGroup { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Name of the user assigned identity.",
        SerializedName = @"ResourceName")]
        public string ResourceName { get; set; } = "";

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

        [ArgumentInfo(
        Required = false,
        Description = @"One complete identity resource ID.",
        SerializedName = @"Ids")]
        public string[]? Ids { get; set; }

        [ArgumentInfo(Required = false, Description = @"Generic update set expressions.", SerializedName = @"Set")]
        public string[]? Set { get; set; }

        [ArgumentInfo(Required = false, Description = @"Generic update add expressions.", SerializedName = @"Add")]
        public string[]? Add { get; set; }

        [ArgumentInfo(Required = false, Description = @"Generic update remove expressions.", SerializedName = @"Remove")]
        public string[]? Remove { get; set; }

        [ArgumentInfo(Required = false, Description = @"Preserve string literals for generic updates.", SerializedName = @"ForceString")]
        public string? ForceString { get; set; }

        internal JsonObject? PatchBodyOverride { get; set; }

        protected override void SetResourceIdArguments(string resourceGroup, string resourceName)
        {
            ResourceGroup = resourceGroup;
            ResourceName = resourceName;
        }

        internal override Task<HttpResponseMessage> SendAsync()
        {
            ApplyIdentityResourceId(Ids);
            return base.SendAsync();
        }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            ValidateIdentityNameArguments(ResourceGroup, ResourceName, Ids);

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            string uri = BuildIdentityUri(ResourceGroup, ResourceName);

            JsonObject requestBody = PatchBodyOverride ?? BuildPatchBody();

            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), uri)
            {
                Content = JsonContent.Create(requestBody, options: JsonOptions)
            };

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        internal JsonObject BuildPatchBody()
        {
            var body = new JsonObject();

            Dictionary<string, string>? tags = ParseTags(Tags);
            if (tags is not null)
            {
                body["tags"] = JsonSerializer.SerializeToNode(tags, JsonOptions);
            }

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
            if (properties.Count > 0)
            {
                body["properties"] = properties;
            }

            return body;
        }
    }
}
