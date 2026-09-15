// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Identity.Models;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Processors
{
    public class ShowIdentityProcessor : IdentityProcessorBase<object, UserAssignedIdentity>
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

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"{ManagementEndpoint}/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourceGroups/{Uri.EscapeDataString(ResourceGroup)}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{Uri.EscapeDataString(ResourceName)}?api-version={IdentityApiVersion}";
    }
}
