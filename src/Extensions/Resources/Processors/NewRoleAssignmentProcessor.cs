// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Atomic processor for ARM <c>RoleAssignments - Create</c>
/// (<c>PUT {scope}/providers/Microsoft.Authorization/roleAssignments/{name}</c>). Takes the
/// fully-qualified <see cref="Scope"/>, the assignment <see cref="RoleAssignmentName"/> (a GUID),
/// and the create <see cref="Properties"/>. ARM returns the created <see cref="RoleAssignment"/>. CLI
/// conveniences such as scope resolution, role-definition-id construction, and assignee resolution
/// live in <c>Customization/NewRoleAssignmentProcessor_Orchestrator</c>.
/// </summary>
public class NewRoleAssignmentProcessor : ResourcesProcessorBase<RoleAssignmentCreateParameters, RoleAssignment>
{
    /// <summary>Fully-qualified scope the assignment is created at, e.g. <c>/subscriptions/{id}</c>.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>The role assignment name (a GUID, unique per assignment).</summary>
    public string RoleAssignmentName { get; set; } = string.Empty;

    /// <summary>The create request's <c>properties</c> payload.</summary>
    public RoleAssignmentCreateProperties Properties { get; set; } = new();

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com{Scope}/providers/Microsoft.Authorization/roleAssignments/{RoleAssignmentName}?api-version=2022-04-01";

    protected override RoleAssignmentCreateParameters? CreateRequestBody() => new() { Properties = Properties };
}
