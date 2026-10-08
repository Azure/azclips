// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Atomic processor for ARM <c>RoleAssignments - Create</c>
/// (<c>PUT {scope}/providers/Microsoft.Authorization/roleAssignments/{name}</c>). Takes the
/// fully-qualified <see cref="Scope"/>, the assignment <see cref="RoleAssignmentName"/> (a GUID),
/// and the create properties as individual primitives, flattened from the wire's nested
/// <c>properties</c> object (see <see cref="RoleAssignmentCreateProperties"/>) for a simpler public
/// surface; <see cref="CreateRequestBody"/> re-nests them under <c>properties</c> on the way out. ARM
/// returns the created <see cref="RoleAssignment"/>. CLI conveniences such as scope resolution,
/// role-definition-id construction, and assignee resolution live in
/// <c>Customization/NewRoleAssignmentProcessor_Orchestrator</c>.
/// </summary>
public class NewRoleAssignmentProcessor : ResourcesProcessorBase<RoleAssignmentCreateParameters, RoleAssignment>
{
    /// <summary>
    /// Fully-qualified scope the assignment is created at, e.g. <c>/subscriptions/{id}</c>. Nullable
    /// (no default) so the orchestrator's scope resolver can distinguish "not bound" (null) from an
    /// explicitly bound but blank value, which must be rejected rather than silently widened to the
    /// subscription. Always non-null by the time <see cref="CreateRequestUri"/> reads it, since
    /// <c>BeforeProcessAsync</c> resolves (or throws) before any request is sent.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>The role assignment name (a GUID, unique per assignment).</summary>
    public string RoleAssignmentName { get; set; } = string.Empty;

    /// <summary>The object (principal) ID the role is assigned to.</summary>
    public string? PrincipalId { get; set; }

    /// <summary>The fully-qualified role definition ID.</summary>
    public string? RoleDefinitionId { get; set; }

    /// <summary>The principal's type (e.g. <c>User</c>, <c>Group</c>, <c>ServicePrincipal</c>).</summary>
    public string? PrincipalType { get; set; }

    /// <summary>An optional free-text description of the assignment.</summary>
    public string? Description { get; set; }

    /// <summary>An optional ABAC condition expression that constrains the assignment.</summary>
    public string? Condition { get; set; }

    /// <summary>
    /// The condition syntax version. Required when <see cref="Condition"/> is set; currently the
    /// only accepted value is <c>2.0</c>.
    /// </summary>
    public string? ConditionVersion { get; set; }

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com{Scope}/providers/Microsoft.Authorization/roleAssignments/{RoleAssignmentName}?api-version=2022-04-01";

    protected override RoleAssignmentCreateParameters? CreateRequestBody() => new()
    {
        Properties = new RoleAssignmentCreateProperties
        {
            PrincipalId = PrincipalId,
            RoleDefinitionId = RoleDefinitionId,
            PrincipalType = PrincipalType,
            Description = Description,
            Condition = Condition,
            ConditionVersion = ConditionVersion,
        },
    };
}
