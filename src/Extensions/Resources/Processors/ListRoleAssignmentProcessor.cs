// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Atomic processor for ARM <c>RoleAssignments - List For Scope</c>
/// (<c>GET {scope}/providers/Microsoft.Authorization/roleAssignments</c>). Takes only what the
/// endpoint accepts natively: the fully-qualified <see cref="Scope"/> path and an optional OData
/// <see cref="Filter"/> (e.g. <c>principalId eq '{id}'</c> or <c>atScope()</c>). ARM returns an
/// envelope <c>{ "value": [...], "nextLink": "..." }</c>; this processor unwraps it and follows
/// <c>nextLink</c> so the CLI serializes the flat array of assignments. CLI conveniences such as
/// subscription/resource-group scoping and assignee resolution live in
/// <c>Customization/ListRoleAssignmentProcessor_Orchestrator</c>.
/// </summary>
public class ListRoleAssignmentProcessor : ResourcesProcessorBase<object, RoleAssignmentListResult>
{
    /// <summary>
    /// Fully-qualified scope the list is rooted at, e.g. <c>/subscriptions/{id}</c>. Nullable (no
    /// default) so the orchestrator's scope resolver can distinguish "not bound" (null) from an
    /// explicitly bound but blank value, which must be rejected rather than silently widened to the
    /// subscription. Always non-null by the time <see cref="CreateRequestUri"/> reads it, since
    /// <c>BeforeProcessAsync</c> resolves (or throws) before any request is sent.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>Optional OData <c>$filter</c> expression applied to the list.</summary>
    public string? Filter { get; set; }

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com{Scope}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01"
        + (string.IsNullOrWhiteSpace(Filter) ? string.Empty : $"&$filter={Uri.EscapeDataString(Filter)}");

    /// <summary>
    /// Exposes the spec-shaped first-page request so a caller that must drive its own transport and
    /// pagination — e.g. the create orchestrator's best-effort idempotency lookup — can reuse this URI.
    /// </summary>
    internal Task<HttpRequestMessage> BuildRequestAsync() => CreateRequestMessage();

    private string? nextLink(RoleAssignmentListResult body) => body.NextLink;
    private IEnumerable<RoleAssignment>? pageItems(RoleAssignmentListResult body) =>
        body.Value is null ? null : FilterPageItems(body.Value);

    /// <summary>
    /// Client-side page filter seam. Spec-pure default returns every item unchanged; the orchestrator
    /// overrides this to apply conveniences ARM cannot filter server-side (e.g. by role definition).
    /// Applied per page so pagination still follows <c>nextLink</c> over the unfiltered result set.
    /// </summary>
    protected virtual IEnumerable<RoleAssignment> FilterPageItems(IEnumerable<RoleAssignment> items) => items;

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(RoleAssignmentListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
        return true;
    }
}
