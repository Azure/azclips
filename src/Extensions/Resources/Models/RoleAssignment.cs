// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models
{
    //
    // Summary:
    //     An Azure RBAC role assignment as ARM returns it. The interesting fields are nested
    //     under "properties" (see RoleAssignmentProperties).
    public class RoleAssignment
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public RoleAssignmentProperties? Properties { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }

    //
    // Summary:
    //     Role assignment properties as ARM returns them, nested under "properties".
    public class RoleAssignmentProperties
    {
        public string? RoleDefinitionId { get; set; }
        public string? PrincipalId { get; set; }
        public string? PrincipalType { get; set; }
        public string? Scope { get; set; }
        public DateTimeOffset? CreatedOn { get; set; }
        public DateTimeOffset? UpdatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public string? DelegatedManagedIdentityResourceId { get; set; }
        public string? Condition { get; set; }
        public string? ConditionVersion { get; set; }
        public string? Description { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

        public override string ToString() => JsonNode.FromObject(this).ToString();
    }
}
