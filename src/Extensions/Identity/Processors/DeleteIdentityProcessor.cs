// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Processors
{
    public class DeleteIdentityProcessor : IdentityProcessorBase
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
        Description = @"One or more complete identity resource IDs.",
        SerializedName = @"Ids")]
        public string[]? Ids { get; set; }

        protected override void SetResourceIdArguments(string resourceGroup, string resourceName)
        {
            ResourceGroup = resourceGroup;
            ResourceName = resourceName;
        }

        protected override async Task<HttpRequestMessage> CreateRequestMessage()
        {
            ValidateIdentityNameArguments(ResourceGroup, ResourceName, Ids);

            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            string uri = BuildIdentityUri(ResourceGroup, ResourceName);
            HttpRequestMessage request = new(HttpMethod.Delete, uri);

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            try
            {
                if (Ids is { Length: > 0 })
                {
                    foreach (string id in Ids)
                    {
                        var parsed = ParseIdentityResourceId(id);
                        SubscriptionId = parsed.SubscriptionId;
                        ResourceGroup = parsed.ResourceGroup;
                        ResourceName = parsed.ResourceName;
                        using HttpResponseMessage deleteResponse = await SendAsync();
                    }
                    return;
                }

                ResolveSubscriptionId();
                using HttpResponseMessage response = await SendAsync();
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}
