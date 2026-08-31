// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Identity.Models;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Processors
{
    public class ShowIdentityProcessor : IdentityProcessorBase<UserAssignedIdentity>
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
        Description = @"One complete identity resource ID.",
        SerializedName = @"Ids")]
        public string[]? Ids { get; set; }

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
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
    }
}
