// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Processors
{
    public class DeleteFederatedCredentialProcessor : IdentityProcessorBase<object, object>
    {
        [ArgumentInfo(
        Required = true,
        Description = @"Name of resource group.",
        SerializedName = @"ResourceGroup")]
        public string ResourceGroup { get; set; } = "";

        [ArgumentInfo(
        Required = true,
        Description = @"The name of the identity resource.",
        SerializedName = @"IdentityName")]
        public string IdentityName { get; set; } = "";

        [ArgumentInfo(
        Required = true,
        Description = @"The name of the federated identity credential resource.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        protected override HttpMethod Method => HttpMethod.Delete;

        protected override string CreateRequestUri() =>
            $"{ManagementEndpoint}/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourceGroups/{Uri.EscapeDataString(ResourceGroup)}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{Uri.EscapeDataString(IdentityName)}/federatedIdentityCredentials/{Uri.EscapeDataString(Name)}?api-version={IdentityApiVersion}";
    }
}
