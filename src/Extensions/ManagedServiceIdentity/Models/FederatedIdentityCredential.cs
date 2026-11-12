// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models
{
    public class FederatedIdentityCredential
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("properties")]
        public FederatedIdentityCredentialProperties? Properties { get; set; }

        [JsonPropertyName("systemData")]
        public SystemData? SystemData { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    public class FederatedIdentityCredentialProperties
    {
        [JsonPropertyName("audiences")]
        public List<string>? Audiences { get; set; }

        [JsonPropertyName("issuer")]
        public string? Issuer { get; set; }

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("claimsMatchingExpression")]
        public ClaimsMatchingExpression? ClaimsMatchingExpression { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    public class ClaimsMatchingExpression
    {
        [JsonPropertyName("languageVersion")]
        public int LanguageVersion { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    public class FederatedIdentityCredentialListResult
    {
        [JsonPropertyName("value")]
        public List<FederatedIdentityCredential>? Value { get; set; }

        [JsonPropertyName("nextLink")]
        public string? NextLink { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
