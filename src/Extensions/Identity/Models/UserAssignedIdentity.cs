// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Models
{
    public class UserAssignedIdentity
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }

        [JsonPropertyName("properties")]
        public UserAssignedIdentityProperties? Properties { get; set; }

        [JsonPropertyName("systemData")]
        public SystemData? SystemData { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    public class UserAssignedIdentityProperties
    {
        [JsonPropertyName("assignmentRestrictions")]
        public AssignmentRestrictions? AssignmentRestrictions { get; set; }

        [JsonPropertyName("isolationScope")]
        public string? IsolationScope { get; set; }

        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; }

        [JsonPropertyName("principalId")]
        public string? PrincipalId { get; set; }

        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    public class IdentityUpdate
    {
        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("tags")]
        public Dictionary<string, string>? Tags { get; set; }

        [JsonPropertyName("properties")]
        public UserAssignedIdentityProperties? Properties { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    public class AssignmentRestrictions
    {
        [JsonPropertyName("providers")]
        public List<string>? Providers { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    public class SystemData
    {
        [JsonPropertyName("createdAt")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("createdBy")]
        public string? CreatedBy { get; set; }

        [JsonPropertyName("createdByType")]
        public string? CreatedByType { get; set; }

        [JsonPropertyName("lastModifiedAt")]
        public string? LastModifiedAt { get; set; }

        [JsonPropertyName("lastModifiedBy")]
        public string? LastModifiedBy { get; set; }

        [JsonPropertyName("lastModifiedByType")]
        public string? LastModifiedByType { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
