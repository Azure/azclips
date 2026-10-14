using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;
using Microsoft.Extensions.AI;

namespace Core.Agent.Tools.Resource.Cache;

/// <summary>
/// Three AI agent tools that READ the local ARM cache (built and
/// refreshed by another component — we never write to it):
///   • find_resources       — fuzzy / substring search by name (cache-only)
///   • get_resource_tree    — hierarchical subtree from a node (cache-only)
///   • get_resource_details — exact identity from cache + live ARM properties
/// </summary>
public sealed class CachedResourceTools
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly ArmDetailsClient _armDetails;
    private readonly IProfileService _profileService;
    private readonly Func<string, IDisposable?>? _activityCreator;

    public CachedResourceTools(
        ArmDetailsClient armDetails, IProfileService profileService,
        Func<string, IDisposable?>? activityCreator = null)
    {
        _armDetails = armDetails;
        _profileService = profileService;
        _activityCreator = activityCreator;

        FindResources = AIFunctionFactory.Create(FindResourcesAsync, new AIFunctionFactoryOptions
        {
            Name = "find_resources",
            Description = "Search the local Azure resource cache by name. " +
                $"`kind` selects which collection to search: {CacheKinds.KindList}. " +
                "When kind is omitted, all kinds are searched and merged by score. " +
                "Use when the user mentions a name and you need to locate it (or " +
                "get suggestions when an exact name doesn't exist — e.g. 'rg-foo " +
                "not found, did you mean rg-foo-eastus?'). Returns up to N candidates " +
                "ranked by match quality (exact > substring > fuzzy/Levenshtein). " +
                "Cache-only, no network call.",
        });

        GetResourceTree = AIFunctionFactory.Create(GetResourceTreeAsync, new AIFunctionFactoryOptions
        {
            Name = "get_resource_tree",
            Description = "Return the hierarchical subtree under a node (cache only). " +
                $"`kind` is one of: {CacheKinds.KindList}, and identifies what type of node " +
                "to root the tree at. Use to answer 'what's in subscription X' or " +
                "'what's in RG Y'. maxDepth limits expansion to avoid returning " +
                "megabytes. Resources are leaves with no further children in this view.",
        });

        GetResourceDetails = AIFunctionFactory.Create(GetResourceDetailsAsync, new AIFunctionFactoryOptions
        {
            Name = "get_resource_details",
            Description = "Return details for one item from the cache. " +
                $"`kind` (optional) is one of: {CacheKinds.KindList} and narrows the search. " +
                "Cache fields (id, type, location, sku, tags, kind, identity, managedBy) " +
                "are always returned. Set live=true ONLY when you need fields that " +
                "aren't in the cache (e.g. properties, provisioningState, endpoints, " +
                "encryption settings) — that triggers one extra ARM GET (~200-500ms). " +
                "Default live=false; pick live=true when answering the user genuinely " +
                "needs deeper detail.",
        });

        All = new[] { FindResources, GetResourceTree, GetResourceDetails };
    }

    /// <summary>AIFunction wrapping the find_resources tool.</summary>
    public AIFunction FindResources { get; }

    /// <summary>AIFunction wrapping the get_resource_tree tool.</summary>
    public AIFunction GetResourceTree { get; }

    /// <summary>AIFunction wrapping the get_resource_details tool.</summary>
    public AIFunction GetResourceDetails { get; }

    /// <summary>All three tools — convenience for agent registration.</summary>
    public IReadOnlyList<AIFunction> All { get; }

    // ── tool implementations ──

    // Point the cache at the caller's current tenant before every read. The
    // active profile is the source of truth; reading it here (rather than
    // pinning once at daemon startup) means a mid-session `az account set`
    // to another tenant is picked up on the next resource query. Cheap: a
    // profile read (small local file) plus an in-memory provider swap.
    private async Task SyncTenantAsync(CancellationToken ct)
    {
        // Best-effort: a corrupt/unreadable profiles.json must not break the read
        // tools' "always return JSON" contract. On failure, leave the tenant unset
        // (cache falls back to root-level lookup) rather than letting the tool throw.
        try
        {
            var profile = await _profileService.GetActiveProfileAsync(ct).ConfigureAwait(false);
            ResourceCache.SetCurrentTenantId(profile?.TenantId);
        }
        catch
        {
            ResourceCache.SetCurrentTenantId(null);
        }
    }

    [Description("Search local cache for resources by name with fuzzy fallback.")]
    private async Task<string> FindResourcesAsync(
        [Description("Name pattern. Exact > substring > Levenshtein typo correction.")]
        string namePattern,
        [Description("Optional kind filter: tenant | subscription | resourceGroup | resource. When omitted, all kinds are searched and results merged by score.")]
        string? kind = null,
        [Description("Optional subscription id or display name to narrow.")]
        string? subscription = null,
        [Description("Optional resource group name to narrow (only meaningful for kind=resource).")]
        string? resourceGroup = null,
        [Description("Max candidates to return. Default 10.")]
        int top = 10,
        CancellationToken ct = default)
    {
        using var scope = ToolInvocationLog.Start("find_resources",
            $"pattern={namePattern} kind={kind} sub={subscription} rg={resourceGroup} top={top}", _activityCreator);
        scope.RecordRequest(RedactedKind(kind));

        await SyncTenantAsync(ct).ConfigureAwait(false);

        // Validate kind early so the model gets a clear error instead of empty hits.
        if (!string.IsNullOrWhiteSpace(kind) && !CacheKinds.TryResolve(kind, out _))
        {
            var err = JsonSerializer.Serialize(new
            {
                error = "unknown_kind",
                provided = kind,
                validKinds = CacheKinds.AllKinds,
            }, Json);
            scope.SetResult($"error=unknown_kind provided={kind}");
            scope.RecordResponse("error");
            return err;
        }

        var filter = (subscription is { Length: > 0 } || resourceGroup is { Length: > 0 })
            ? new ScopeFilter(subscription, resourceGroup)
            : null;

        var stats = ResourceCache.Current.Stats();
        var matches = ResourceCache.Current.FindResources(namePattern, kind, filter, top);

        var result = new
        {
            query = new { namePattern, kind, subscription, resourceGroup, top },
            cache = new { stats.Resources, loadedAtUtc = stats.LoadedAtUtc },
            matchCount = matches.Count,
            matches = matches.Select(m => new
            {
                name = m.Resource.Name,
                type = m.Resource.Type,
                location = m.Resource.Location,
                resourceGroup = m.Resource.ResourceGroup,
                subscriptionId = m.Resource.SubscriptionId,
                id = m.Resource.Id,
                matchKind = m.Match,
                score = Math.Round(m.Score, 3),
            }).ToArray(),
        };
        var json = JsonSerializer.Serialize(result, Json);
        var summary = matches.Count == 0
            ? "matchCount=0"
            : $"matchCount={matches.Count} — top: {matches[0].Resource.Name} ({matches[0].Match}, score={Math.Round(matches[0].Score, 3)})";
        scope.SetResult(summary);
        scope.RecordResponse(matches.Count == 0 ? "not_found" : "found");
        return json;
    }

    [Description("Return cached subtree from a node.")]
    private async Task<string> GetResourceTreeAsync(
        [Description("Name, full ARM id, subscription id/name, or tenant id. Use kind to disambiguate.")]
        string nameOrId,
        [Description("Required kind: tenant | subscription | resourceGroup | resource. Identifies what type of node to root the tree at.")]
        string kind,
        [Description("Max depth from this node. Defaults to 2 (sub→RG, RG→resource). Set higher to drill further.")]
        int maxDepth = 2,
        [Description("Optional subscription scope when kind=resourceGroup and the RG name repeats across subs.")]
        string? subscription = null,
        CancellationToken ct = default)
    {
        using var scope = ToolInvocationLog.Start("get_resource_tree",
            $"id={nameOrId} kind={kind} depth={maxDepth}", _activityCreator);
        scope.RecordRequest(RedactedKind(kind));

        await SyncTenantAsync(ct).ConfigureAwait(false);

        if (!CacheKinds.TryResolve(kind, out _))
        {
            var err = JsonSerializer.Serialize(new
            {
                error = "unknown_kind",
                provided = kind,
                validKinds = CacheKinds.AllKinds,
            }, Json);
            scope.SetResult($"error=unknown_kind provided={kind}");
            scope.RecordResponse("error");
            return err;
        }

        string json;
        string summary;
        try
        {
            var filter = subscription is { Length: > 0 } ? new ScopeFilter(subscription, null) : null;

            (string id, string kindNorm, string name) root = kind.ToLowerInvariant() switch
            {
                "tenant" => ResolveTenant(nameOrId),
                "subscription" => ResolveSub(nameOrId),
                "resourcegroup" => ResolveRg(nameOrId, filter),
                "resource" => ResolveRes(nameOrId, filter),
                _ => throw new ArgumentException($"Unknown kind '{kind}'."),
            };

            var tree = ResourceCache.Current.GetTree(root.id, root.kindNorm, root.name, maxDepth);
            json = JsonSerializer.Serialize(new { root = root.kindNorm, maxDepth = maxDepth, tree }, Json);
            summary = $"root={root.kindNorm}:{root.name} depth={maxDepth}";
            scope.RecordResponse("found");
        }
        catch (AmbiguousMatchException ex)
        {
            json = JsonSerializer.Serialize(new { error = "ambiguous_match", candidates = ex.CandidateIds }, Json);
            summary = $"error=ambiguous_match candidates={ex.CandidateIds?.Count ?? 0}";
            scope.RecordResponse("ambiguous");
        }
        catch (Exception ex)
        {
            json = JsonSerializer.Serialize(new { error = ex.GetType().Name, message = ex.Message }, Json);
            summary = $"error={ex.GetType().Name}";
            // Resolvers report a missing node as InvalidOperationException.
            scope.RecordResponse(ex is InvalidOperationException ? "not_found" : "error");
        }
        scope.SetResult(summary);
        return json;
    }

    [Description("Return cache row + optionally one live ARM GET for one item.")]
    private async Task<string> GetResourceDetailsAsync(
        [Description("Name or full ARM id. Use kind/scope to disambiguate.")]
        string nameOrId,
        [Description("Optional kind filter: tenant | subscription | resourceGroup | resource. When omitted, all kinds are searched.")]
        string? kind = null,
        [Description("Optional subscription id or name to scope.")]
        string? subscription = null,
        [Description("Optional resource group to scope (only meaningful for kind=resource).")]
        string? resourceGroup = null,
        [Description("Set true to also fetch live ARM body (properties, provisioningState, endpoints). Adds ~200-500ms. Default false.")]
        bool live = false,
        CancellationToken ct = default)
    {
        using var scope = ToolInvocationLog.Start("get_resource_details",
            $"id={nameOrId} kind={kind} sub={subscription} rg={resourceGroup} live={live}", _activityCreator);
        scope.RecordRequest($"{RedactedKind(kind)} live={(live ? "true" : "false")}");

        await SyncTenantAsync(ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(kind) && !CacheKinds.TryResolve(kind, out _))
        {
            var err = JsonSerializer.Serialize(new
            {
                error = "unknown_kind",
                provided = kind,
                validKinds = CacheKinds.AllKinds,
            }, Json);
            scope.SetResult($"error=unknown_kind provided={kind}");
            scope.RecordResponse("error");
            return err;
        }

        var filter = (subscription is { Length: > 0 } || resourceGroup is { Length: > 0 })
            ? new ScopeFilter(subscription, resourceGroup)
            : null;

        string json;
        string summary;
        try
        {
            var hit = ResourceCache.Current.ResolveResource(nameOrId, kind, filter);
            if (hit is null)
            {
                var suggestions = ResourceCache.Current.FindResources(nameOrId, kind, filter, top: 5);
                json = JsonSerializer.Serialize(new
                {
                    error = "not_found",
                    nameOrId,
                    suggestions = suggestions.Select(m => new { m.Resource.Name, m.Resource.Id, m.Match, score = Math.Round(m.Score, 3) }),
                }, Json);
                summary = suggestions.Count == 0
                    ? "not_found, suggestions=0"
                    : $"not_found, suggestions={suggestions.Count} — top: {suggestions[0].Resource.Name}";
                scope.RecordResponse("not_found");
            }
            else
            {
                var cacheRow = new
                {
                    source = "cache",
                    id = hit.Id,
                    name = hit.Name,
                    type = hit.Type,
                    location = hit.Location,
                    subscriptionId = hit.SubscriptionId,
                    tenantId = hit.TenantId,
                    resourceGroup = hit.ResourceGroup,
                    kind = hit.Kind,
                    sku = hit.Sku,
                    tags = hit.Tags,
                    identity = hit.Identity,
                    managedBy = hit.ManagedBy,
                };

                if (!live)
                {
                    json = JsonSerializer.Serialize(new { cache = cacheRow, live = (object?)null }, Json);
                    summary = $"cache hit: {hit.Name} ({hit.Type})";
                    scope.RecordResponse("found");
                }
                else
                {
                    var liveBody = await _armDetails.GetByIdAsync(hit.Id, ct).ConfigureAwait(false);
                    json = JsonSerializer.Serialize(new
                    {
                        cache = cacheRow,
                        live = new { source = "arm-live", body = liveBody },
                    }, Json);
                    summary = $"cache+live hit: {hit.Name} ({hit.Type})";
                    scope.RecordResponse("found");
                }
            }
        }
        catch (AmbiguousMatchException ex)
        {
            json = JsonSerializer.Serialize(new
            {
                error = "ambiguous_match",
                hint = "Pass type / subscription / resourceGroup to narrow.",
                candidates = ex.CandidateIds,
            }, Json);
            summary = $"error=ambiguous_match candidates={ex.CandidateIds?.Count ?? 0}";
            scope.RecordResponse("ambiguous");
        }
        scope.SetResult(summary);
        return json;
    }

    // Only the closed kind vocabulary is recorded; names, ids and scopes stay local.
    private static string RedactedKind(string? kind) =>
        string.IsNullOrWhiteSpace(kind) ? "kind=any"
        : CacheKinds.TryResolve(kind, out _) ? $"kind={kind.ToLowerInvariant()}" : "kind=invalid";

    // ── kind resolvers ──

    private static (string id, string kind, string name) ResolveTenant(string nameOrId)
    {
        var t = ResourceCache.Current.Tenants
            .FirstOrDefault(t => string.Equals(t.TenantId, nameOrId, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(t.DisplayName, nameOrId, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(t.DefaultDomain, nameOrId, StringComparison.OrdinalIgnoreCase));
        if (t is null) throw new InvalidOperationException($"tenant '{nameOrId}' not found in cache.");
        return (t.TenantId, "tenant", t.DisplayName ?? t.TenantId);
    }

    private static (string id, string kind, string name) ResolveSub(string nameOrId)
    {
        var s = ResourceCache.Current.ResolveSubscription(nameOrId)
            ?? throw new InvalidOperationException($"subscription '{nameOrId}' not found in cache.");
        return (s.SubscriptionId, "subscription", s.DisplayName ?? s.SubscriptionId);
    }

    private static (string id, string kind, string name) ResolveRg(string nameOrId, ScopeFilter? scope)
    {
        var rg = ResourceCache.Current.ResolveResourceGroup(nameOrId, scope)
            ?? throw new InvalidOperationException($"resource group '{nameOrId}' not found in cache.");
        return (rg.Id, "resourceGroup", rg.Name);
    }

    private static (string id, string kind, string name) ResolveRes(string nameOrId, ScopeFilter? scope)
    {
        var r = ResourceCache.Current.ResolveResource(nameOrId, kind: null, scope)
            ?? throw new InvalidOperationException($"resource '{nameOrId}' not found in cache.");
        return (r.Id, "resource", r.Name);
    }
}
