// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models;

namespace Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Processors
{
    public class ListFederatedCredentialProcessor : IdentityProcessorBase<object, FederatedIdentityCredentialListResult>
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
        Required = false,
        Description = @"A skip token is used to continue retrieving items after an operation returns a partial result. If a previous response contains a nextLink element, the value of the nextLink element will include a skipToken parameter that specifies a starting point to use for subsequent calls.",
        SerializedName = @"SkipToken")]
        public string? SkipToken { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Number of records to return.",
        SerializedName = @"Top")]
        public int? Top { get; set; }

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri()
        {
            var uri = $"{ManagementEndpoint}/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourceGroups/{Uri.EscapeDataString(ResourceGroup)}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{Uri.EscapeDataString(IdentityName)}/federatedIdentityCredentials?api-version={IdentityApiVersion}";
            if (!string.IsNullOrEmpty(SkipToken))
            {
                uri += $"&$skiptoken={Uri.EscapeDataString(SkipToken)}";
            }
            if (Top is not null)
            {
                uri += $"&$top={Top}";
            }
            return uri;
        }

        private string? NextLink(FederatedIdentityCredentialListResult body) => body.NextLink;
        private IEnumerable<FederatedIdentityCredential>? PageItems(FederatedIdentityCredentialListResult body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(FederatedIdentityCredentialListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(PageItems, NextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
