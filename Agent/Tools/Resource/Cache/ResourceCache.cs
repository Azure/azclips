using System.Collections.Concurrent;
using System.Text.Json;
using Core.Abstractions;

namespace Core.Agent.Tools.Resource.Cache;

/// <summary>
/// Process-wide reader over the on-disk ARM cache under
/// <c>~/.azclips/agent/arm/&lt;tenantId&gt;/</c>. Tenants live at
/// the cache root because they span tenants; subs / rgs / resources live in
/// a per-tenant subdirectory so switching tenant just switches directory.
///
/// The cache itself (build / refresh) is owned by <see cref="ArmCacheRefresher"/>,
/// which every read path kicks in a fire-and-forget background task so the
/// NEXT query sees fresh data. Missing files degrade to empty lists.
///
/// Indexes rebuild when any cache file's mtime changes (cheap stat call);
/// invocations are short-lived, the model session is the longest consumer.
/// </summary>
public sealed class ResourceCache
{
    private static readonly Lazy<ResourceCache> _instance = new(() => new ResourceCache());
    public static ResourceCache Current => _instance.Value;

    /// <summary>
    /// Wires the singleton's background refresher and current-tenant provider.
    /// Idempotent; only the first non-null argument on each slot sticks so a
    /// second call from a test host can't tear down live wiring. Call from
    /// agent startup after auth is available.
    /// </summary>
    public static ArmCacheRefresher? ConfigureSingleton(Func<string?>? currentTenantId, ArmCacheRefresher? refresher)
    {
        var c = Current;
        lock (c._lock)
        {
            c._currentTenantId ??= currentTenantId;
            c._refresher ??= refresher;
            // Force reload so the next EnsureLoaded picks up the new tenant.
            c._loadedAt = DateTime.MinValue;
            return c._refresher;
        }
    }

    public static ArmCacheRefresher GetOrCreateRefresher(Func<ArmCacheRefresher> create)
        => Current.GetOrCreateRefresherCore(create);

    internal ArmCacheRefresher GetOrCreateRefresherCore(Func<ArmCacheRefresher> create)
    {
        lock (_lock)
        {
            // A closing Agent owner retains its actual tasks, but cannot service a later host.
            // First-writer-wins still applies while the installed refresher is live.
            if (_refresher is null || _refresher.IsHostClosing)
                _refresher = create();
            return _refresher;
        }
    }

    /// <summary>
    /// Sets the tenant the cache reads from, overwriting any previous value.
    /// Cheap: just swaps an in-memory provider — no file I/O, no TTL change,
    /// no network. The next EnsureLoaded notices the tenant differs from the
    /// one it last loaded and re-points at that tenant's cache subdirectory
    /// (and kicks a background refresh if it's cold).
    ///
    /// Called by the resource-reading tools on every invocation so a mid-session
    /// tenant switch (e.g. the user runs `az account set` while an interactive
    /// agent is open) is reflected on the next resource query, instead of being
    /// pinned to whatever tenant was active when the daemon started.
    /// </summary>
    public static void SetCurrentTenantId(string? tenantId)
    {
        var c = Current;
        lock (c._lock)
        {
            c._currentTenantId = () => tenantId;
        }
    }

    public static string DefaultCacheDir => HomePaths.GetDefaultArmCacheDirectory();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _root;
    private Func<string?>? _currentTenantId;
    private ArmCacheRefresher? _refresher;
    private readonly object _lock = new();

    private string? _loadedTenantId;
    private DateTime _loadedAt = DateTime.MinValue;
    private Dictionary<string, DateTime> _fileMtimes = new();

    public IReadOnlyList<CachedTenant> Tenants { get; private set; } = Array.Empty<CachedTenant>();
    public IReadOnlyList<CachedSubscription> Subscriptions { get; private set; } = Array.Empty<CachedSubscription>();
    public IReadOnlyList<CachedResourceGroup> ResourceGroups { get; private set; } = Array.Empty<CachedResourceGroup>();
    public IReadOnlyList<CachedResource> Resources { get; private set; } = Array.Empty<CachedResource>();

    // Indexes
    private Dictionary<string, CachedResource> _resByIdCi = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CachedResourceGroup> _rgByIdCi = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CachedSubscription> _subById = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<CachedResource>> _resByNameLower = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<CachedResourceGroup>> _rgByNameLower = new(StringComparer.OrdinalIgnoreCase);
    private SortedSet<string> _resIdsSorted = new(StringComparer.OrdinalIgnoreCase);

    public ResourceCache(
        string? cacheDir = null,
        Func<string?>? currentTenantId = null,
        ArmCacheRefresher? refresher = null)
    {
        _root = cacheDir ?? DefaultCacheDir;
        _currentTenantId = currentTenantId;
        _refresher = refresher;
    }

    /// <summary>Forces a reload on next query.</summary>
    public void Invalidate()
    {
        lock (_lock) { _loadedAt = DateTime.MinValue; }
    }

    private void EnsureLoaded()
    {
        // Kick the background refresh outside the lock. Fire-and-forget; the
        // next call sees fresh data. Failing refreshes are swallowed inside
        // the refresher — never a reason to fail a read.
        _refresher?.KickAsync();

        lock (_lock)
        {
            var tenantId = _currentTenantId?.Invoke();
            var tenantDir = string.IsNullOrEmpty(tenantId) ? _root : Path.Combine(_root, tenantId!);

            // Tenants file lives at the cache root (shared across tenants);
            // the per-tenant subdirectory owns everything else.
            var probe = new[]
            {
                Path.Combine(_root,     "tenants.json"),
                Path.Combine(tenantDir, "subscriptions.json"),
                Path.Combine(tenantDir, "resource-groups.json"),
                Path.Combine(tenantDir, "resources.json"),
            };
            var current = probe.ToDictionary(
                p => p,
                p => File.Exists(p) ? File.GetLastWriteTimeUtc(p) : DateTime.MinValue);

            var tenantChanged = !string.Equals(_loadedTenantId, tenantId, StringComparison.OrdinalIgnoreCase);
            bool changed = tenantChanged
                || current.Count != _fileMtimes.Count
                || current.Any(kv => !_fileMtimes.TryGetValue(kv.Key, out var t) || t != kv.Value);
            if (!changed && _loadedAt != DateTime.MinValue) return;

            Tenants        = LoadFile<CachedTenant>(Path.Combine(_root,     "tenants.json"));
            Subscriptions  = LoadFile<CachedSubscription>(Path.Combine(tenantDir, "subscriptions.json"));
            ResourceGroups = LoadFile<CachedResourceGroup>(Path.Combine(tenantDir, "resource-groups.json"));
            Resources      = LoadFile<CachedResource>(Path.Combine(tenantDir, "resources.json"));
            BuildIndexes();

            _fileMtimes = current;
            _loadedTenantId = tenantId;
            _loadedAt = DateTime.UtcNow;
        }
    }

    private List<T> LoadFile<T>(string path)
    {
        if (!File.Exists(path)) return new List<T>();
        try
        {
            // FileShare.ReadWrite | Delete lets ArmCacheRefresher's atomic
            // tmp+rename replace this file while a reader still has it open;
            // without Delete-sharing, File.Move(overwrite:true) fails on
            // Windows with "access denied" during a concurrent refresh.
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var env = JsonSerializer.Deserialize<CacheFile<T>>(fs, Json);
            return env?.Items ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    private void BuildIndexes()
    {
        _subById = Subscriptions.ToDictionary(s => s.SubscriptionId, s => s, StringComparer.OrdinalIgnoreCase);

        _rgByIdCi = ResourceGroups.ToDictionary(r => r.Id, r => r, StringComparer.OrdinalIgnoreCase);
        _rgByNameLower = ResourceGroups
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        _resByIdCi = new Dictionary<string, CachedResource>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in Resources) _resByIdCi[r.Id] = r;

        _resByNameLower = Resources
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        _resIdsSorted = new SortedSet<string>(Resources.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
    }

    // ── Public query helpers used by the three AIFunction tools ──

    public CacheStats Stats()
    {
        EnsureLoaded();
        return new CacheStats(
            Tenants.Count, Subscriptions.Count, ResourceGroups.Count, Resources.Count,
            _loadedAt == DateTime.MinValue ? null : _loadedAt);
    }

    /// <summary>
    /// find_resources backing impl. Always returns at most <paramref name="top"/>
    /// rows. If <paramref name="namePattern"/> is null/empty, returns the first
    /// <paramref name="top"/> rows that match the type/scope filter.
    /// </summary>
    public IReadOnlyList<MatchResult> FindResources(
        string? namePattern,
        string? kind,
        ScopeFilter? scope,
        int top)
    {
        EnsureLoaded();

        // Dispatch on kind via the central registry. When kind is null/empty
        // we search every registered kind and merge by score; when it's
        // provided but unknown we deliberately return empty so the tool
        // wrapper can surface a structured error listing valid kinds.
        IEnumerable<KindSpec> kindsToSearch;
        if (string.IsNullOrWhiteSpace(kind))
        {
            kindsToSearch = CacheKinds.Registry.Values;
        }
        else if (CacheKinds.TryResolve(kind, out var spec))
        {
            kindsToSearch = new[] { spec };
        }
        else
        {
            return Array.Empty<MatchResult>();
        }

        var perKind = new List<IReadOnlyList<MatchResult>>();
        foreach (var spec in kindsToSearch)
        {
            var candidates = spec.AllItems(this);
            if (scope is not null) candidates = ApplyScope(candidates, scope);
            perKind.Add(RankByName(candidates, namePattern, top));
        }

        // Single-kind path: already correctly capped, return as-is.
        if (perKind.Count == 1) return perKind[0];

        // Multi-kind: re-merge across kinds and re-cap to `top`.
        return perKind
            .SelectMany(x => x)
            .OrderByDescending(m => m.Score)
            .Take(top)
            .ToList();
    }

    private static IReadOnlyList<MatchResult> RankByName(
        IEnumerable<CachedResource> candidates,
        string? namePattern,
        int top)
    {
        var pat = namePattern?.Trim();
        var materialized = candidates as IList<CachedResource> ?? candidates.ToList();

        if (string.IsNullOrEmpty(pat))
        {
            return materialized
                .Take(top)
                .Select(c => new MatchResult(c, "scope-only", 1.0))
                .ToList();
        }

        var patLower = pat.ToLowerInvariant();

        var exact = materialized.Where(c => string.Equals(c.Name, pat, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count > 0)
        {
            return exact.Take(top).Select(c => new MatchResult(c, "exact", 1.0)).ToList();
        }

        // A substring hit counts only when it identifies one resource; an ambiguous
        // fragment falls through to the typo tier.
        var substr = materialized
            .Where(c => c.Name.Contains(pat, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        if (substr.Count == 1)
        {
            return [new MatchResult(substr[0], "substring", SubstringScore(substr[0].Name, patLower))];
        }

        // Typo tier: edit distance <= 2, or <= 1 for patterns of 4 chars or fewer.
        var maxDistance = patLower.Length <= 4 ? 1 : 2;
        return materialized
            .Select(c => (Resource: c, Distance: Lev(c.Name.ToLowerInvariant(), patLower)))
            .Where(x => x.Distance <= maxDistance)
            .Select(x => new MatchResult(x.Resource, "fuzzy",
                1.0 - (x.Distance / (double)Math.Max(x.Resource.Name.Length, patLower.Length))))
            .OrderByDescending(m => m.Score)
            .Take(top)
            .ToList();
    }

    /// <summary>
    /// Resolves an identifier to a single resource. <paramref name="nameOrId"/>
    /// can be a full ARM id, an exact name, or a unique substring. Throws
    /// <see cref="AmbiguousMatchException"/> when multiple resources match;
    /// returns null when none match.
    /// </summary>
    public CachedResource? ResolveResource(string nameOrId, string? kind, ScopeFilter? scope)
    {
        EnsureLoaded();

        // 1) Looks like an ARM id?
        if (nameOrId.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase))
        {
            if (_resByIdCi.TryGetValue(nameOrId, out var byResId)) return byResId;
            // Also check RG ARM ids — same /subscriptions/X/resourceGroups/Y form.
            if (_rgByIdCi.TryGetValue(nameOrId, out var byRgId)) return CacheKinds.ProjectResourceGroup(byRgId);
        }

        // 2) Single FindResources call — RankByName short-circuits to exact /
        // substring / fuzzy in that order, so the Match field on the first
        // row tells us which tier we landed in.
        var matches = FindResources(nameOrId, kind, scope, top: 25);
        if (matches.Count == 0) return null;

        if (matches[0].Match == "exact")
        {
            var exact = matches.Where(m => m.Match == "exact").ToList();
            if (exact.Count == 1) return exact[0].Resource;
            throw new AmbiguousMatchException(exact.Select(m => m.Resource.Id).ToList());
        }

        // 3) Substring/fuzzy fallback — already capped by FindResources.
        if (matches.Count == 1) return matches[0].Resource;
        throw new AmbiguousMatchException(matches.Select(m => m.Resource.Id).ToList());
    }

    public CachedResourceGroup? ResolveResourceGroup(string nameOrId, ScopeFilter? scope)
    {
        EnsureLoaded();

        if (nameOrId.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase)
            && _rgByIdCi.TryGetValue(nameOrId, out var byId))
        {
            return byId;
        }

        if (!_rgByNameLower.TryGetValue(nameOrId, out var bucket) || bucket.Count == 0)
        {
            return null;
        }

        var filtered = scope?.Subscription is { Length: > 0 } subFilter
            ? bucket.Where(r => MatchesSub(r.SubscriptionId, subFilter)).ToList()
            : bucket;

        if (filtered.Count == 1) return filtered[0];
        if (filtered.Count > 1)
        {
            throw new AmbiguousMatchException(filtered.Select(r => r.Id).ToList());
        }
        return null;
    }

    public CachedSubscription? ResolveSubscription(string nameOrId)
    {
        EnsureLoaded();
        if (_subById.TryGetValue(nameOrId, out var bySubId)) return bySubId;
        var matches = Subscriptions
            .Where(s => string.Equals(s.DisplayName, nameOrId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 1) return matches[0];
        if (matches.Count > 1)
        {
            throw new AmbiguousMatchException(matches.Select(s => s.SubscriptionId).ToList());
        }
        return null;
    }

    /// <summary>
    /// Returns the in-cache subtree of <paramref name="rootId"/>. Used by
    /// get_resource_tree. The tree is built by ARM-id prefix match — works
    /// at tenant / subscription / RG level. For leaf resources the tree
    /// is just the node itself with empty children.
    /// </summary>
    public TreeNode GetTree(string rootId, string rootKind, string rootName, int maxDepth)
    {
        EnsureLoaded();
        var root = new TreeNode(rootName, rootKind, rootId);
        BuildTreeChildren(root, maxDepth, currentDepth: 0);
        return root;
    }

    private void BuildTreeChildren(TreeNode parent, int maxDepth, int currentDepth)
    {
        if (currentDepth >= maxDepth) return;

        switch (parent.Kind)
        {
            case "tenant":
                foreach (var s in Subscriptions.Where(s => string.Equals(s.TenantId, parent.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    var node = new TreeNode(s.DisplayName ?? s.SubscriptionId, "subscription", s.SubscriptionId);
                    parent.Children.Add(node);
                    BuildTreeChildren(node, maxDepth, currentDepth + 1);
                }
                break;

            case "subscription":
                foreach (var rg in ResourceGroups.Where(r => string.Equals(r.SubscriptionId, parent.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    var node = new TreeNode(rg.Name, "resourceGroup", rg.Id) { Location = rg.Location };
                    parent.Children.Add(node);
                    BuildTreeChildren(node, maxDepth, currentDepth + 1);
                }
                break;

            case "resourceGroup":
                var rgPrefix = parent.Id + "/providers/";
                foreach (var r in Resources.Where(r => r.Id.StartsWith(rgPrefix, StringComparison.OrdinalIgnoreCase)))
                {
                    parent.Children.Add(new TreeNode(r.Name, "resource", r.Id) { ResourceType = r.Type, Location = r.Location });
                }
                break;

            case "resource":
                // Leaf — no in-cache children. (Sub-resources like VM extensions
                // would require live ARM, out of scope for the cache reader.)
                break;
        }
    }

    // ── helpers ──

    private IEnumerable<CachedResource> ApplyScope(IEnumerable<CachedResource> src, ScopeFilter scope)
    {
        if (!string.IsNullOrWhiteSpace(scope.Subscription))
        {
            src = src.Where(r => MatchesSub(r.SubscriptionId, scope.Subscription!));
        }
        if (!string.IsNullOrWhiteSpace(scope.ResourceGroup))
        {
            src = src.Where(r => string.Equals(r.ResourceGroup, scope.ResourceGroup, StringComparison.OrdinalIgnoreCase));
        }
        return src;
    }

    private bool MatchesSub(string subId, string filter)
    {
        if (string.Equals(subId, filter, StringComparison.OrdinalIgnoreCase)) return true;
        if (_subById.TryGetValue(subId, out var sub)
            && string.Equals(sub.DisplayName, filter, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static double SubstringScore(string name, string patLower)
    {
        var nameLower = name.ToLowerInvariant();
        var idx = nameLower.IndexOf(patLower, StringComparison.Ordinal);
        if (idx < 0) return 0;
        // Higher score for prefix and shorter name.
        var prefixBonus = idx == 0 ? 0.3 : 0.0;
        var lengthRatio = patLower.Length / (double)Math.Max(name.Length, 1);
        return Math.Min(0.99, 0.5 + prefixBonus + 0.2 * lengthRatio);
    }

    private static int Lev(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }
}

public sealed record CacheStats(
    int Tenants, int Subscriptions, int ResourceGroups, int Resources, DateTime? LoadedAtUtc);

public sealed record MatchResult(CachedResource Resource, string Match, double Score);

public sealed class TreeNode
{
    public string Name { get; }
    public string Kind { get; }
    public string Id { get; }
    public string? ResourceType { get; set; }
    public string? Location { get; set; }
    public List<TreeNode> Children { get; } = new();

    public TreeNode(string name, string kind, string id)
    {
        Name = name;
        Kind = kind;
        Id = id;
    }
}

public sealed class AmbiguousMatchException : Exception
{
    public IReadOnlyList<string> CandidateIds { get; }
    public AmbiguousMatchException(IReadOnlyList<string> candidateIds)
        : base($"Ambiguous identifier matches {candidateIds.Count} resources.")
    {
        CandidateIds = candidateIds;
    }
}
