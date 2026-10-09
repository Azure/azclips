// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// CLI customization over the spec-shaped <see cref="NewRoleAssignmentProcessor"/>. Resolves the
/// az-style conveniences that are not part of the ARM create body into the atomic's
/// <see cref="NewRoleAssignmentProcessor.Scope"/>, <see cref="NewRoleAssignmentProcessor.RoleAssignmentName"/>,
/// <see cref="NewRoleAssignmentProcessor.PrincipalId"/>, and
/// <see cref="NewRoleAssignmentProcessor.RoleDefinitionId"/>:
/// <list type="bullet">
/// <item>Scope is the grant target. An explicit scope is used as-is; otherwise it is built from the
/// subscription, optionally narrowed by <see cref="ResourceGroupName"/>. An explicit scope and a
/// resource group are mutually exclusive.</item>
/// <item><see cref="Assignee"/> / <see cref="AssigneeObjectId"/> supply the principal ID. Both accept an
/// object (principal) ID (GUID) only; name/UPN/service-principal resolution needs a Microsoft Graph
/// lookup this ARM-only path does not perform.</item>
/// <item>The inherited <see cref="NewRoleAssignmentProcessor.RoleDefinitionId"/> is bound directly from
/// the CLI as a role definition GUID or an already fully-qualified role definition ID, then expanded
/// in place to the fully-qualified form rooted at the scope's subscription. Role name resolution is
/// not yet supported.</item>
/// <item>A missing <see cref="Name"/> is generated (a fresh GUID).</item>
/// <item>The inherited <see cref="NewRoleAssignmentProcessor.Condition"/> /
/// <see cref="NewRoleAssignmentProcessor.ConditionVersion"/> are bound directly from the CLI, then
/// normalized in place (version defaulted/validated against the condition).</item>
/// <item><see cref="NewRoleAssignmentProcessor.PrincipalType"/> and
/// <see cref="NewRoleAssignmentProcessor.Description"/> are pure pass-through CLI inputs with no
/// resolution step, so they bind directly to the inherited properties with nothing left to do here.</item>
/// </list>
/// </summary>
public class NewRoleAssignmentProcessor_Orchestrator : NewRoleAssignmentProcessor
{
    public string SubscriptionId { get; set; } = string.Empty;

    public string? ResourceGroupName { get; set; }

    public string? Assignee { get; set; }

    public string? AssigneeObjectId { get; set; }

    public string? Name { get; set; }

    // The only condition syntax version currently supported.
    private const string DefaultConditionVersion = "2.0";

    // The ARM path segment that precedes a role definition GUID. Shared by the validity check and the
    // expansion of a bare GUID so the two cannot drift apart.
    private const string RoleDefinitionsSegment = "/providers/Microsoft.Authorization/roleDefinitions/";

    // True when no name was supplied, so ResolveName mints one. Gates idempotent conflict handling: a
    // generated name means the caller only wanted the grant to exist, so returning the pre-existing
    // assignment satisfies them; an explicit name asked for a specific assignment, so the conflict surfaces.
    private bool ShouldGenerateName => string.IsNullOrWhiteSpace(Name);

    protected override async Task BeforeProcessAsync()
    {
        string scope = RoleAssignmentScope.Resolve(SubscriptionId, Scope, ResourceGroupName);
        Scope = scope;
        RoleAssignmentName = ResolveName();
        PrincipalId = ResolvePrincipalId();
        RoleDefinitionId = ResolveRoleDefinitionId(scope);
        (Condition, ConditionVersion) = ResolveCondition();

        await base.BeforeProcessAsync();
    }

    /// <summary>
    /// Overrides the standard pipeline to make create idempotent. The request is sent inline (not via
    /// <c>base.ProcessAsync</c>) so an expected <c>409 RoleAssignmentExists</c> is not routed through
    /// <c>EnsureSuccessAsync</c>, which would publish a failure event before the conflict could be handled.
    /// The method reads as four steps: send, write-on-success, recover-on-expected-conflict, else surface
    /// the failure normally. Mirrors <see cref="ExistsResourceGroupProcessor_Orchestrator"/>.
    /// </summary>
    public override async Task ProcessAsync()
    {
        try
        {
            await BeforeProcessAsync();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            using HttpResponseMessage response = await SendCreateRequestAsync();
            string? rawBody = response.Content is null ? null : await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                await WriteCreatedAsync(response, rawBody);
                return;
            }

            if (await TryReturnExistingAsync(response, rawBody))
            {
                return;
            }
            await response.EnsureSuccessAsync(Publisher);
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    private async Task<HttpResponseMessage> SendCreateRequestAsync()
    {
        using HttpRequestMessage request = await CreateRequestMessage();
        HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);
        return response;
    }

    /// <summary>
    /// Idempotent-create recovery from an expected conflict. ARM returns <c>409 RoleAssignmentExists</c>
    /// when the same principal, role, and scope are already assigned under a different name. When the name
    /// was auto-generated the caller did not ask for a specific assignment, so the pre-existing one is
    /// looked up and written (returning <c>true</c>) instead of failing — mirroring classic
    /// <c>az role assignment create</c>. Returns <c>false</c> for any other response, or when the existing
    /// assignment can't be resolved (a permission gap or race), so the caller surfaces the conflict. With an
    /// explicit <c>--name</c>/<c>-Name</c> this is deliberately skipped: returning a differently-named
    /// assignment than the one the caller asked to create would be surprising, so the conflict is surfaced.
    /// </summary>
    private async Task<bool> TryReturnExistingAsync(HttpResponseMessage response, string? rawBody)
    {
        if (!ShouldGenerateName || !IsRoleAssignmentExistsConflict(response.StatusCode, rawBody))
        {
            return false;
        }

        RoleAssignment? existing = await FindExistingAssignmentAsync();
        if (existing is null)
        {
            return false;
        }

        await Publisher.Publish(Events.Warning, new EventData
        {
            Message = "A role assignment for this principal, role, and scope already exists; returning the existing assignment.",
        });
        await WriteOutput(existing);
        return true;
    }

    // Writes the created assignment with the same no-content / empty-body / invalid-JSON handling the base
    // processor applies, so bypassing base.ProcessAsync doesn't leak a bare serializer exception.
    private async Task WriteCreatedAsync(HttpResponseMessage response, string? rawBody)
    {
        if (response.Content is null)
        {
            await Publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
            return;
        }

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            await Publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
            return;
        }

        RoleAssignment? created;
        try
        {
            created = JsonSerializer.Deserialize<RoleAssignment>(rawBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
            throw new InvalidOperationException($"Failed to deserialize response body into {nameof(RoleAssignment)}. Raw body: {rawBody}", ex);
        }

        if (created is not null)
        {
            await WriteOutput(created);
        }
    }

    // The ARM error *code* (parsed from the response body) is required, not a substring of the flattened
    // message, so an unrelated 409 that merely mentions "RoleAssignmentExists" is never swallowed.
    private static bool IsRoleAssignmentExistsConflict(HttpStatusCode status, string? rawBody)
    {
        if (status != HttpStatusCode.Conflict || string.IsNullOrWhiteSpace(rawBody))
        {
            return false;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(rawBody);
            return doc.RootElement.TryGetProperty("error", out JsonElement error)
                && error.TryGetProperty("code", out JsonElement code)
                && code.ValueKind == JsonValueKind.String
                && string.Equals(code.GetString(), "RoleAssignmentExists", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Best-effort lookup of the assignment that caused the conflict: pages through the principal's
    /// assignments at the scope (following every <c>nextLink</c>) and returns the role/scope match.
    /// Cancellation propagates; any other failure returns null so the caller surfaces the original conflict.
    /// </summary>
    private async Task<RoleAssignment?> FindExistingAssignmentAsync()
    {
        HttpRequestMessage? request = await BuildLookupRequestAsync();
        try
        {
            while (request is not null)
            {
                RoleAssignmentListResult? page = await FetchLookupPageAsync(request);
                if (page is null)
                {
                    return null;
                }

                if (page.Value?.FirstOrDefault(MatchesExisting) is { } match)
                {
                    return match;
                }

                request.Dispose();
                request = string.IsNullOrWhiteSpace(page.NextLink) ? null : new HttpRequestMessage(HttpMethod.Get, page.NextLink);
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            // Cancellation must surface, not be masked by the best-effort fallback below.
            throw;
        }
        catch
        {
            // Best-effort: never let a lookup failure mask the original conflict.
            return null;
        }
        finally
        {
            request?.Dispose();
        }
    }

    // Builds the first-page lookup request: the principal's assignments at the scope.
    private async Task<HttpRequestMessage> BuildLookupRequestAsync()
    {
        ListRoleAssignmentProcessor lister = CreateExtendedProcessor<ListRoleAssignmentProcessor>(p =>
        {
            p.Scope = Scope;
            p.Filter = $"principalId eq '{PrincipalId}'";
        });
        return await lister.BuildRequestAsync();
    }

    /// <summary>
    /// Sends one lookup page over a raw transport — not the atomic's <c>SendAsync</c> — so a lookup failure
    /// doesn't publish a <c>ResourceOperationFailed</c> event ahead of the original conflict, then traces and
    /// parses it. Returns null to stop the lookup on a non-success status, an empty body, or malformed JSON.
    /// </summary>
    private async Task<RoleAssignmentListResult?> FetchLookupPageAsync(HttpRequestMessage request)
    {
        using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        // Each page publishes its own trace; HttpPipeline.SendAsync does not emit it.
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);

        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            return null;
        }

        string raw = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RoleAssignmentListResult>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Matches an assignment to this create's principal, scope, and role (by trailing role GUID, so a
    // built-in role rooted at a different scope still compares equal).
    private bool MatchesExisting(RoleAssignment assignment) =>
        string.Equals(assignment.Properties?.PrincipalId, PrincipalId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(assignment.Properties?.Scope, ExpectedScope, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(RoleDefinitionTail(assignment.Properties?.RoleDefinitionId), RoleDefinitionTail(RoleDefinitionId), StringComparison.OrdinalIgnoreCase);

    // The scope ARM reports on the existing assignment. Normalize stores the tenant-root scope as "" (its
    // request-path form, which builds "/providers/..."), but ARM identifies tenant-root assignments with
    // "/"; remap so a tenant-root create still matches its existing assignment. Mirrors the list orchestrator.
    private string ExpectedScope => string.IsNullOrEmpty(Scope) ? "/" : Scope;

    // The trailing GUID segment of a (possibly fully-qualified) role definition ID.
    private static string RoleDefinitionTail(string? roleDefinitionId) =>
        string.IsNullOrEmpty(roleDefinitionId)
            ? string.Empty
            : roleDefinitionId[(roleDefinitionId.LastIndexOf('/') + 1)..];

    // Produces the ARM assignment name from the Name input: mints a fresh GUID when none was supplied,
    // otherwise returns the caller's trimmed-and-validated GUID (reusing the shared RequireGuid validator).
    private string ResolveName()
    {
        if (ShouldGenerateName)
        {
            return Guid.NewGuid().ToString();
        }

        return RequireGuid(Name!, value => $"The role assignment name '{value}' must be a GUID.");
    }

    /// <summary>
    /// Pairs the condition with a version: rejects an explicitly bound but blank condition (treating
    /// it as "omitted" would silently turn an intended conditional grant into an unconditional one),
    /// defaults the version to <c>2.0</c> when a condition is supplied without one, rejects a version
    /// supplied without a condition, and rejects any version other than the supported <c>2.0</c>.
    /// </summary>
    private (string? Condition, string? ConditionVersion) ResolveCondition()
    {
        bool hasCondition = Condition is not null;
        if (hasCondition && string.IsNullOrWhiteSpace(Condition))
        {
            throw AzclipsErrorException.UsageError("Condition must not be empty.");
        }

        bool hasVersion = !string.IsNullOrWhiteSpace(ConditionVersion);

        if (!hasCondition)
        {
            if (hasVersion)
            {
                throw AzclipsErrorException.UsageError(
                    "A condition version was specified without a condition. Specify a condition, or omit the condition version.");
            }

            return (null, null);
        }

        string condition = Condition!.Trim();

        if (!hasVersion)
        {
            return (condition, DefaultConditionVersion);
        }

        string version = ConditionVersion!.Trim();
        if (!string.Equals(version, DefaultConditionVersion, StringComparison.Ordinal))
        {
            throw AzclipsErrorException.UsageError(
                $"The condition version '{version}' is not supported. The only supported condition version is '{DefaultConditionVersion}'.");
        }

        return (condition, version);
    }

    /// <summary>
    /// Resolves the principal object ID. <see cref="AssigneeObjectId"/> is trusted; <see cref="Assignee"/>
    /// is accepted only when it is already an object ID (GUID). Exactly one must be supplied.
    /// </summary>
    private string ResolvePrincipalId()
    {
        bool hasAssignee = !string.IsNullOrWhiteSpace(Assignee);
        bool hasObjectId = !string.IsNullOrWhiteSpace(AssigneeObjectId);

        if (hasAssignee && hasObjectId)
        {
            throw AzclipsErrorException.UsageError("Specify only one assignee.");
        }

        if (!hasAssignee && !hasObjectId)
        {
            throw AzclipsErrorException.MissingRequiredParameters(
                "A principal is required. Specify the principal's object ID (GUID) to assign the role to.");
        }

        return hasObjectId
            ? RequireGuid(AssigneeObjectId!, value => $"The assignee object ID '{value}' is not a valid object ID (GUID).")
            : RequireGuid(Assignee!, value =>
                $"The assignee '{value}' is not an object ID (GUID). Name, UPN, and service-principal resolution is not yet supported; " +
                "specify the principal's object ID (GUID).");
    }

    // Trims a value and validates it is a GUID; returns the trimmed value, or throws the caller-supplied
    // usage-error message (with the trimmed value already substituted in) otherwise. Shared by the name and
    // principal resolvers.
    private static string RequireGuid(string rawValue, Func<string, string> errorMessage)
    {
        string value = rawValue.Trim();
        if (!Guid.TryParse(value, out _))
        {
            throw AzclipsErrorException.UsageError(errorMessage(value));
        }

        return value;
    }

    /// <summary>
    /// Expands the required <see cref="RoleDefinitionId"/> into a fully-qualified role definition ID.
    /// A value that is already a fully-qualified role definition ID (ending in a GUID) is used as-is; a
    /// bare GUID is rooted at the grant scope's subscription (or the scope itself for management-group /
    /// tenant scopes). Role name resolution is not yet supported.
    /// </summary>
    private string ResolveRoleDefinitionId(string scope)
    {
        if (string.IsNullOrWhiteSpace(RoleDefinitionId))
        {
            throw AzclipsErrorException.MissingRequiredParameters("A role is required. Specify the role definition ID (GUID).");
        }

        string value = RoleDefinitionId.Trim();
        if (value.StartsWith('/'))
        {
            if (IsRoleDefinitionId(value))
            {
                return value;
            }

            throw AzclipsErrorException.UsageError(
                $"The role '{value}' is not a valid role definition ID. Specify a role definition GUID, or a fully-qualified " +
                "role definition ID ending in '/providers/Microsoft.Authorization/roleDefinitions/{guid}'.");
        }

        if (!Guid.TryParse(value, out _))
        {
            throw AzclipsErrorException.UsageError(
                $"The role '{value}' is not a role definition ID. Role name resolution is not yet supported; " +
                "specify a role definition GUID, or a fully-qualified role definition ID ending in " +
                "'/providers/Microsoft.Authorization/roleDefinitions/{guid}'.");
        }

        return $"{RoleDefinitionRoot(scope)}{RoleDefinitionsSegment}{value}";
    }

    // True when the value is a fully-qualified role definition ID whose final segment is a GUID.
    private static bool IsRoleDefinitionId(string value)
    {
        int index = value.IndexOf(RoleDefinitionsSegment, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        string tail = value[(index + RoleDefinitionsSegment.Length)..].TrimEnd('/');
        return Guid.TryParse(tail, out _);
    }

    // Roots the role definition at the scope's subscription when present, else at the scope itself.
    private static string RoleDefinitionRoot(string scope)
    {
        const string subscriptions = "/subscriptions/";
        if (scope.StartsWith(subscriptions, StringComparison.OrdinalIgnoreCase))
        {
            int next = scope.IndexOf('/', subscriptions.Length);
            return next < 0 ? scope : scope[..next];
        }

        return scope;
    }
}


