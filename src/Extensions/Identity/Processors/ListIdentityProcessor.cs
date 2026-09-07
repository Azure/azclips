// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Identity.Models;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Processors
{
    public class ListIdentityProcessor : IdentityProcessorBase<object, UserAssignedIdentityListResult>
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Name of resource group.",
        SerializedName = @"ResourceGroup")]
        public string? ResourceGroup { get; set; }

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri()
        {
            string scope = string.IsNullOrWhiteSpace(ResourceGroup)
                ? $"subscriptions/{Uri.EscapeDataString(SubscriptionId)}"
                : $"subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourceGroups/{Uri.EscapeDataString(ResourceGroup)}";
            return $"{ManagementEndpoint}/{scope}/providers/Microsoft.ManagedIdentity/userAssignedIdentities?api-version={IdentityApiVersion}";
        }

        private string? NextLink(UserAssignedIdentityListResult body) => body.NextLink;
        private IEnumerable<UserAssignedIdentity>? PageItems(UserAssignedIdentityListResult body) => body.Value;
        
        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(UserAssignedIdentityListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(PageItems, NextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
