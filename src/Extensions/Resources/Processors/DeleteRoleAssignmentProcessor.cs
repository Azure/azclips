// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Atomic processor for ARM <c>RoleAssignments - Delete</c>
/// (<c>DELETE {scope}/providers/Microsoft.Authorization/roleAssignments/{name}</c>). Takes the
/// fully-qualified <see cref="Scope"/> and the assignment <see cref="RoleAssignmentName"/> (a GUID).
/// ARM returns the deleted <see cref="RoleAssignment"/> (200 OK), or no content (204, when the
/// assignment did not exist — deleting a nonexistent assignment is not an error). CLI conveniences
/// such as scope resolution live in <c>Customization/DeleteRoleAssignmentProcessor_Orchestrator</c>.
/// </summary>
public class DeleteRoleAssignmentProcessor : ResourcesProcessorBase<object, RoleAssignment>
{
    /// <summary>
    /// Fully-qualified scope the assignment is deleted from, e.g. <c>/subscriptions/{id}</c>. Nullable
    /// (no default) so the orchestrator's scope resolver can distinguish "not bound" (null) from an
    /// explicitly bound but blank value, which must be rejected rather than silently widened to the
    /// subscription. Always non-null by the time <see cref="CreateRequestUri"/> reads it, since
    /// <c>BeforeProcessAsync</c> resolves (or throws) before any request is sent.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>The role assignment name (a GUID, unique per assignment) to delete.</summary>
    public string RoleAssignmentName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Delete;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com{Scope}/providers/Microsoft.Authorization/roleAssignments/{RoleAssignmentName}?api-version=2022-04-01";
}
