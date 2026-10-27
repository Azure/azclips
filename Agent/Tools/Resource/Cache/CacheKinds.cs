using System.Diagnostics.CodeAnalysis;

namespace Core.Agent.Tools.Resource.Cache;

/// <summary>
/// Single source of truth for "what searchable cache kinds exist". Each
/// kind maps 1:1 to a JSON file under <c>%LOCALAPPDATA%/.azclips/agent/arm/</c> and
/// drives parameter validation, dispatch, and tool descriptions across
/// the three diagnostic tools (find_resources, get_resource_tree,
/// get_resource_details).
///
/// To add a new searchable kind (e.g. role-assignments, providers):
///   1. Add a CachedXxx record + JSON file loader in ResourceCache.
///   2. Add a projection from CachedXxx → CachedResource here (the
///      common shape carried by MatchResult).
///   3. Add a single Registry entry below.
/// All three tool descriptions, parameter validation, and the
/// "no kind → search all" merge path pick up the new kind automatically.
/// </summary>
internal static class CacheKinds
{
    /// <summary>Canonical kind names. Use these instead of bare strings.</summary>
    public static class Names
    {
        public const string Tenant        = "tenant";
        public const string Subscription  = "subscription";
        public const string ResourceGroup = "resourceGroup";
        public const string Resource      = "resource";
    }

    public static readonly IReadOnlyDictionary<string, KindSpec> Registry =
        new Dictionary<string, KindSpec>(StringComparer.OrdinalIgnoreCase)
        {
            [Names.Tenant] = new KindSpec(
                Kind:     Names.Tenant,
                FileName: "tenants.json",
                AllItems: cache => cache.Tenants.Select(ProjectTenant)),

            [Names.Subscription] = new KindSpec(
                Kind:     Names.Subscription,
                FileName: "subscriptions.json",
                AllItems: cache => cache.Subscriptions.Select(ProjectSubscription)),

            [Names.ResourceGroup] = new KindSpec(
                Kind:     Names.ResourceGroup,
                FileName: "resource-groups.json",
                AllItems: cache => cache.ResourceGroups.Select(ProjectResourceGroup)),

            [Names.Resource] = new KindSpec(
                Kind:     Names.Resource,
                FileName: "resources.json",
                AllItems: cache => cache.Resources),
        };

    /// <summary>
    /// Stable enumeration of the registered kind names, e.g. for tool
    /// descriptions: <c>$"kind: {string.Join(" | ", AllKinds)}"</c>.
    /// </summary>
    public static IReadOnlyList<string> AllKinds { get; } = Registry.Keys.ToArray();

    /// <summary>Pipe-joined kind list for tool descriptions.</summary>
    public static string KindList { get; } = string.Join(" | ", AllKinds);

    /// <summary>
    /// Case-insensitive lookup with normalization to the canonical kind
    /// name. Returns false (and a list of valid kinds) for unknown input.
    /// </summary>
    public static bool TryResolve(string? input, [NotNullWhen(true)] out KindSpec? spec)
    {
        spec = null;
        if (string.IsNullOrWhiteSpace(input)) return false;
        return Registry.TryGetValue(input.Trim(), out spec);
    }

    // ── Projections ──
    //
    // MatchResult carries CachedResource as its lowest-common shape.
    // Tenants/Subscriptions/ResourceGroups don't have all the same fields,
    // so unfilled ones are null. Consumers that need the native shape
    // should use ResourceCache.Tenants/Subscriptions/ResourceGroups directly.

    public static CachedResource ProjectTenant(CachedTenant t) => new(
        Id:             t.TenantId,
        Name:           t.DisplayName ?? t.TenantId,
        Type:           "Microsoft.Tenant",
        Location:       null,
        SubscriptionId: string.Empty,
        TenantId:       t.TenantId,
        ResourceGroup:  null,
        Kind:           t.DefaultDomain,
        Sku:            null,
        Tags:           null,
        Identity:       null,
        ManagedBy:      null);

    public static CachedResource ProjectSubscription(CachedSubscription s) => new(
        Id:             s.SubscriptionId,
        Name:           s.DisplayName ?? s.SubscriptionId,
        Type:           "Microsoft.Subscription",
        Location:       null,
        SubscriptionId: s.SubscriptionId,
        TenantId:       s.TenantId,
        ResourceGroup:  null,
        Kind:           s.State,
        Sku:            null,
        Tags:           null,
        Identity:       null,
        ManagedBy:      null);

    public static CachedResource ProjectResourceGroup(CachedResourceGroup rg) => new(
        Id:             rg.Id,
        Name:           rg.Name,
        Type:           "Microsoft.Resources/resourceGroups",
        Location:       rg.Location,
        SubscriptionId: rg.SubscriptionId,
        TenantId:       rg.TenantId,
        ResourceGroup:  null,
        Kind:           null,
        Sku:            null,
        Tags:           rg.Tags,
        Identity:       null,
        ManagedBy:      null);
}

internal sealed record KindSpec(
    string Kind,
    string FileName,
    Func<ResourceCache, IEnumerable<CachedResource>> AllItems);
