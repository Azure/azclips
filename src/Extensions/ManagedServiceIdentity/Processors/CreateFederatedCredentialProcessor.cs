// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models;

namespace Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Processors
{
    public class CreateFederatedCredentialProcessor : IdentityProcessorBase<FederatedIdentityCredential, FederatedIdentityCredential>
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

        [ArgumentInfo(
        Required = false,
        Description = @"The list of audiences that can appear in the issued token.",
        SerializedName = @"Audiences")]
        public List<string> Audiences { get; set; } = ["api://AzureADTokenExchange"];

        [ArgumentInfo(
        Required = false,
        Description = @"The URL of the issuer to be trusted.",
        SerializedName = @"Issuer")]
        public string? Issuer { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The identifier of the external software workload within the external identity provider. Either 'Subject' or 'ClaimsMatchingExpressionValue' must be defined, but not both.",
        SerializedName = @"Subject")]
        public string? Subject { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Specifies the language version of the claims matching expression.",
        SerializedName = @"ClaimsMatchingExpressionVersion")]
        public int? ClaimsMatchingExpressionVersion { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The wildcard-based expression for matching incoming claims. Cannot be used with 'Subject'.",
        SerializedName = @"ClaimsMatchingExpressionValue")]
        public string? ClaimsMatchingExpressionValue { get; set; }

        protected override HttpMethod Method => HttpMethod.Put;

        protected override string CreateRequestUri() =>
            $"{ManagementEndpoint}/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourceGroups/{Uri.EscapeDataString(ResourceGroup)}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{Uri.EscapeDataString(IdentityName)}/federatedIdentityCredentials/{Uri.EscapeDataString(Name)}?api-version={IdentityApiVersion}";

        protected override FederatedIdentityCredential CreateRequestBody() =>
            new()
            {
                Properties = new FederatedIdentityCredentialProperties
                {
                    Audiences = Audiences,
                    Issuer = Issuer,
                    Subject = Subject,
                    // Version/value pairing (both-or-neither) is already enforced by FederatedCredentialValidator
                    // in BeforeProcessAsync, so Version alone is sufficient to detect presence.
                    ClaimsMatchingExpression = ClaimsMatchingExpressionVersion is null ? null
                    : new ClaimsMatchingExpression
                    {
                        LanguageVersion = ClaimsMatchingExpressionVersion.Value,
                        Value = ClaimsMatchingExpressionValue
                    }
                }
            };
    }
}
