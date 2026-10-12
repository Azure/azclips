// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Models;

//
// Summary:
//     Request body for creating a role assignment
//     (PUT {scope}/providers/Microsoft.Authorization/roleAssignments/{name}). Only the
//     properties that are set are serialized, so ARM never receives the read-only fields it
//     returns on a role assignment (createdOn, updatedBy, and so on).
public class RoleAssignmentCreateParameters
{
    [JsonPropertyName("properties")]
    public RoleAssignmentCreateProperties Properties { get; set; } = new();
}

public class RoleAssignmentCreateProperties
{
    // The role definition ID (fully qualified, e.g.
    // /subscriptions/{sub}/providers/Microsoft.Authorization/roleDefinitions/{guid}).
    public string? RoleDefinitionId { get; set; }

    // The object (principal) ID the role is assigned to.
    public string? PrincipalId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrincipalType { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Condition { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConditionVersion { get; set; }
}
