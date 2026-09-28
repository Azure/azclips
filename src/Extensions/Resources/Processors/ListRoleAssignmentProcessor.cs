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
    /// <summary>Fully-qualified scope the list is rooted at, e.g. <c>/subscriptions/{id}</c>.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Optional OData <c>$filter</c> expression applied to the list.</summary>
    public string? Filter { get; set; }

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com{Scope}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01"
        + (string.IsNullOrWhiteSpace(Filter) ? string.Empty : $"&$filter={Uri.EscapeDataString(Filter)}");

    private string? nextLink(RoleAssignmentListResult body) => body.NextLink;
    private RoleAssignment[]? pageItems(RoleAssignmentListResult body) => body.Value;

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(RoleAssignmentListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
        return true;
    }
}
