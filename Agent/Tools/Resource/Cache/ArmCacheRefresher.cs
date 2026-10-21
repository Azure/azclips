using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Core.Abstractions;

namespace Core.Agent.Tools.Resource.Cache;

/// <summary>
/// Fire-and-forget background refresher for the on-disk ARM cache under
/// <c>%LOCALAPPDATA%/.azclips/agent/arm/&lt;tenantId&gt;/</c>.
///
/// <para>Wired to <see cref="ResourceCache"/>'s top-of-query hooks: when the
/// user asks a resource question we synchronously answer from whatever is on
/// disk (fast, possibly stale) and kick a refresh in the background so the
/// NEXT query sees fresh data. Nothing waits on this task.</para>
///
/// <para>Per-file TTL policy (<see cref="TenantTtl"/> etc.). tenants + subs
/// use existence checks instead of TTL — they change rarely and re-adding a
/// tenant/sub is what triggers a refresh. RG and resource lists TTL because
/// they churn.</para>
///
/// <para>Cross-process safety: each cache file has its own <c>.lock</c> under
/// <c>.lock/</c>; a refresher that can't acquire the lock in ~10 s assumes
/// another process is doing the work and bows out. The lock (not the
/// on-disk <c>cachedAt</c>) is what coalesces peer refreshers — the
/// <c>cachedAt</c> field is only stamped once the fresh payload is on disk,
/// so a transient ARG failure doesn't pin peers stale for a full TTL.</para>
/// </summary>
public sealed class ArmCacheRefresher
{
    // TTLs mirror PP.md L156-161 (wzl's measured baseline for a ~15k-resource tenant).
    internal static readonly TimeSpan TenantTtl = TimeSpan.Zero;        // never expires; presence check
    internal static readonly TimeSpan SubscriptionTtl = TimeSpan.Zero;  // never expires; presence check
    internal static readonly TimeSpan ResourceGroupTtl = TimeSpan.FromDays(1);
    internal static readonly TimeSpan ResourceTtl = TimeSpan.FromHours(8);

    // Wall-clock budget for one full tenant refresh (tenants→subs→rgs→resources).
    // Enforced per-refresh via a linked CTS in RefreshTenantAsync so it holds
    // regardless of the injected HttpClient's own Timeout — most call sites pass
    // the DI singleton (Timeout=100s), which is shorter than a large-tenant ARG
    // sweep needs and must not silently cap this background work. Sized as a
    // conservative multiple of the measured ~15k-resource baseline so ~10x-larger
    // tenants still complete.
    internal static readonly TimeSpan RefreshBudget = TimeSpan.FromSeconds(300);

    private const int SchemaVersion = 1;
    private const string ArmScope = "https://management.azure.com/.default";
    private const string TenantsListUri = "https://management.azure.com/tenants?api-version=2022-12-01";
    private const string ArgQueryUri = "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2024-04-01";
    private const int ArgPageSize = 1000;

    // KQL for the three ARG-backed caches. Projections match CachedSubscription /
    // CachedResourceGroup / CachedResource shape so the JSON round-trips without
    // extra mapping.
    private const string SubQuery =
        "ResourceContainers | where type =~ 'microsoft.resources/subscriptions' " +
        "| project subscriptionId, displayName=name, tenantId, state=tostring(properties.state)";
    private const string RgQuery =
        "ResourceContainers | where type =~ 'microsoft.resources/subscriptions/resourcegroups' " +
        "| project id, name, location, subscriptionId, tenantId, tags";
    private const string ResourceQuery =
        "Resources | project id, name, type, location, subscriptionId, tenantId, resourceGroup, kind, sku, tags, identity, managedBy";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IAuthService _authService;
    private readonly IProfileService _profileService;
    private readonly HttpClient _http;
    private readonly string _cacheRoot;

    // Process-local dedup so the many query paths that all call KickAsync
    // don't stack 5+ refreshes for the same tenant. Keyed by tenantId; a
    // running refresh keeps its Task in here until it completes.
    private readonly ConcurrentDictionary<string, Task> _inFlight = new(StringComparer.OrdinalIgnoreCase);

    public ArmCacheRefresher(
        IAuthService authService,
        IProfileService profileService,
        HttpClient? http = null,
        string? cacheRoot = null,
        Action<string>? progress = null)
    {
        _authService = authService;
        _profileService = profileService;
        // Self-constructed fallback only (tests / direct construction). Production
        // call sites inject the DI singleton, whose Timeout we deliberately don't
        // touch — the per-refresh budget is enforced by the linked CTS in
        // RefreshTenantAsync instead, so it applies no matter who owns the client.
        _http = http ?? new HttpClient { Timeout = RefreshBudget };
        _cacheRoot = cacheRoot ?? HomePaths.GetDefaultArmCacheDirectory();
        _progress = progress;
    }

    private readonly Action<string>? _progress;
    private void Report(string message) => _progress?.Invoke(message);

    /// <summary>
    /// Fire-and-forget: start a refresh for the active profile's tenant/sub
    /// if one isn't already running for that tenant. Returns immediately.
    /// Exceptions inside the background task are swallowed and surfaced only
    /// via <see cref="LastError"/> for troubleshooting — a failing refresh
    /// must not crash the caller.
    /// </summary>
    public void KickAsync()
    {
        // Fire-and-forget: the whole point is to leave a fresh cache behind for
        // the NEXT query, so it must outlive the caller. It deliberately takes
        // no CancellationToken — a caller can't (and shouldn't) cancel a
        // detached background refresh. Runs on CancellationToken.None; failures
        // surface via LastError.
        //
        // No de-dup needed here: RefreshOnceAsync coalesces per-tenant via
        // _inFlight and RefreshTenantAsync guards with a file lock, so repeated
        // kicks never run a second real refresh — the extra outer tasks just
        // await the shared work and cost nothing worth optimizing.
        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshOnceAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LastError = ex;
            }
        }, CancellationToken.None);
    }

    /// <summary>Exposed for troubleshooting / tests; last background failure.</summary>
    public Exception? LastError { get; private set; }

    /// <summary>
    /// Full one-shot refresh: tenants (if missing), then the active tenant's
    /// subs / rgs / resources. Public for tests; production callers use
    /// <see cref="KickAsync"/>.
    /// </summary>
    public async Task RefreshOnceAsync(CancellationToken ct = default)
    {
        var profile = await _profileService.GetActiveProfileAsync(ct).ConfigureAwait(false);
        if (profile is null || string.IsNullOrEmpty(profile.TenantId))
        {
            // Not signed in — nothing to refresh. Reader-side tools handle the
            // missing tenant directory by returning empty lists (LoadFile
            // returns [] when the file doesn't exist), so the user sees "no
            // resources found" rather than an explicit error until they log in.
            return;
        }

        var tenantId = profile.TenantId!;

        // Coalesce concurrent kicks for the same tenant. The GetOrAdd factory
        // runs synchronously, so it returns the same Task everyone can await.
        //
        // The shared background task uses CancellationToken.None so a single
        // caller cancelling their await does NOT cancel the refresh for every
        // other caller that coalesced onto the same task. Each caller applies
        // their own ct only to their own await via WaitAsync — they see
        // OperationCanceledException locally while the shared work continues.
        //
        // Removal is tied to the task's *actual* completion via ContinueWith,
        // not to any caller's finally. If A cancels while the shared task is
        // still running, the entry stays in _inFlight and B still coalesces
        // onto the same running task instead of starting a second refresh.
        var task = _inFlight.GetOrAdd(tenantId, tid =>
        {
            var t = Task.Run(() => RefreshTenantAsync(tid, CancellationToken.None));
            _ = t.ContinueWith(
                completed => _inFlight.TryRemove(tid, out _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return t;
        });
        await task.WaitAsync(ct).ConfigureAwait(false);
    }

    private async Task RefreshTenantAsync(string tenantId, CancellationToken outerCt)
    {
        // Cap the whole tenant sweep at RefreshBudget regardless of the injected
        // HttpClient's own Timeout. Linked to the caller's token so an explicit
        // cancel still wins; on budget expiry every in-flight ARM/ARG call sees
        // OperationCanceledException and the refresh bails rather than hanging.
        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        budgetCts.CancelAfter(RefreshBudget);
        var ct = budgetCts.Token;

        Directory.CreateDirectory(_cacheRoot);

        var tenantDir = Path.Combine(_cacheRoot, tenantId);
        Directory.CreateDirectory(tenantDir);

        // Tenants file lives at the cache root (shared across tenants); refresh
        // only when it's missing (presence check, not TTL). ARG can only see
        // the current tenant, so this uses ARM /tenants.
        var tenantsPath = Path.Combine(_cacheRoot, "tenants.json");
        if (!File.Exists(tenantsPath))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Report("→ tenants (ARM /tenants)");
            await RefreshTenantsListAsync(tenantsPath, ct).ConfigureAwait(false);
            Report($"  tenants done in {sw.Elapsed.TotalSeconds:F1}s");
        }

        // Subscriptions: presence check on file. Refresh when missing.
        // Special-cased because rgs / resources need the sub id list in their
        // ARG POST body — without it, ARG returns cross-tenant data with an
        // unbounded skipToken chain (observed: 692k rows and still going).
        var subsPath = Path.Combine(tenantDir, "subscriptions.json");
        if (!File.Exists(subsPath))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Report("→ subscriptions (ARG)");
            await RefreshArgFileAsync(
                subsPath, SubQuery, SubscriptionTtl, tenantId,
                subscriptionIds: null, skipPersistIfEmpty: true, ct).ConfigureAwait(false);
            Report($"  subscriptions done in {sw.Elapsed.TotalSeconds:F1}s");
        }

        // Load the sub id list for the subsequent scoped queries.
        var subIds = LoadSubscriptionIds(subsPath);
        Report($"  scoping subsequent queries to {subIds.Count} subscriptions");

        // Zero subs = nothing to enumerate. Falling through would send an
        // unscoped ARG query (payload has no subscriptions field) and
        // reintroduce the cross-tenant runaway this file guards against.
        if (subIds.Count == 0)
        {
            Report("  no subscriptions to scope over; skipping rg/resource refresh");
            return;
        }

        // Resource groups: TTL 1 day.
        var rgsPath = Path.Combine(tenantDir, "resource-groups.json");
        if (NeedsTtlRefresh(rgsPath, ResourceGroupTtl))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Report("→ resource-groups (ARG)");
            await RefreshArgFileAsync(
                rgsPath, RgQuery, ResourceGroupTtl, tenantId, subIds,
                skipPersistIfEmpty: false, ct).ConfigureAwait(false);
            Report($"  resource-groups done in {sw.Elapsed.TotalSeconds:F1}s");
        }

        // Resources: TTL 8 hours.
        var resPath = Path.Combine(tenantDir, "resources.json");
        if (NeedsTtlRefresh(resPath, ResourceTtl))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Report("→ resources (ARG, paged)");
            await RefreshArgFileAsync(
                resPath, ResourceQuery, ResourceTtl, tenantId, subIds,
                skipPersistIfEmpty: false, ct).ConfigureAwait(false);
            Report($"  resources done in {sw.Elapsed.TotalSeconds:F1}s");
        }
    }

    // Open a cache file for reading while allowing the refresher's atomic
    // File.Move(overwrite:true) to replace it out from under us. FileShare.Read
    // (File.OpenRead's default) blocks the writer on Windows and produces
    // "access denied" during a concurrent refresh; ReadWrite + Delete restores
    // POSIX-like replace-while-open semantics.
    private static FileStream OpenForSharedRead(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

    private static IReadOnlyList<string> LoadSubscriptionIds(string subsPath)
    {
        if (!File.Exists(subsPath)) return Array.Empty<string>();
        try
        {
            using var fs = OpenForSharedRead(subsPath);
            using var doc = JsonDocument.Parse(fs);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();
            var ids = new List<string>(items.GetArrayLength());
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("subscriptionId", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                {
                    var id = idEl.GetString();
                    if (!string.IsNullOrEmpty(id)) ids.Add(id!);
                }
            }
            return ids;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool NeedsTtlRefresh(string path, TimeSpan ttl)
    {
        if (!File.Exists(path)) return true;
        try
        {
            using var fs = OpenForSharedRead(path);
            using var doc = JsonDocument.Parse(fs);
            if (!doc.RootElement.TryGetProperty("cachedAt", out var el)) return true;
            if (!el.TryGetDateTime(out var cachedAt)) return true;
            return DateTime.UtcNow > cachedAt.ToUniversalTime() + ttl;
        }
        catch
        {
            return true;
        }
    }

    private async Task RefreshTenantsListAsync(string path, CancellationToken ct)
    {
        var lockPath = Path.Combine(_cacheRoot, ".lock", "tenants.lock");
        using var lockStream = await FileLock.TryAcquireAsync(lockPath, FileLock.DefaultWait, ct).ConfigureAwait(false);
        if (lockStream is null) return;                    // another process is refreshing; bow out
        if (File.Exists(path)) return;                     // that other process finished; nothing to do

        var token = await GetArmTokenAsync(tenantId: null, ct).ConfigureAwait(false);
        using var req = new HttpRequestMessage(HttpMethod.Get, TenantsListUri);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

        var items = new List<CachedTenant>();
        if (doc.RootElement.TryGetProperty("value", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in arr.EnumerateArray())
            {
                var tid = GetString(t, "tenantId") ?? string.Empty;
                if (string.IsNullOrEmpty(tid)) continue;
                items.Add(new CachedTenant(
                    tid,
                    GetString(t, "displayName"),
                    GetString(t, "defaultDomain")));
            }
        }

        // Presence-check semantics: an empty tenants list would mean "0
        // tenants forever" on next run (File.Exists → skip refresh). If the
        // ARM call really returned zero items (transient permission drop,
        // API glitch, brand-new tenant), refuse to persist — next call will
        // re-fetch. Real success writes a real list.
        if (items.Count == 0)
        {
            Report("  tenants API returned 0 items; not persisting so next call retries.");
            return;
        }

        await WriteCacheFileAsync(path, TenantTtl, items, ct).ConfigureAwait(false);
    }

    private async Task RefreshArgFileAsync(
        string path,
        string query,
        TimeSpan ttl,
        string tenantId,
        IReadOnlyList<string>? subscriptionIds,
        bool skipPersistIfEmpty,
        CancellationToken ct)
    {
        var lockPath = Path.Combine(Path.GetDirectoryName(path)!, ".lock", $"{Path.GetFileNameWithoutExtension(path)}.lock");
        using var lockStream = await FileLock.TryAcquireAsync(lockPath, FileLock.DefaultWait, ct).ConfigureAwait(false);
        if (lockStream is null) return;

        if (ttl == TimeSpan.Zero)
        {
            if (File.Exists(path)) return;
        }
        else if (!NeedsTtlRefresh(path, ttl))
        {
            return;
        }

        // Query first, THEN write. Stamping a fresh cachedAt marker before the
        // query would make NeedsTtlRefresh() skip the file for another TTL
        // window if the ARG call throws — a transient failure would look like
        // a successful refresh and pin the cache stale until TTL expiry (up to
        // 8 h for resources). The .lock acquired above already coalesces peer
        // refreshers; the marker was double-protection at the cost of
        // correctness under failure.
        var items = await QueryArgAsync(query, tenantId, subscriptionIds, ct).ConfigureAwait(false);

        // Presence-check caches (subscriptions) must not persist an empty
        // result — the next run would see the file exist, skip refresh, load
        // 0 subs, and pin every downstream query in a half-broken state
        // until the user manually deletes the file. TTL caches (rgs,
        // resources) may legitimately be empty (tenant with no resources),
        // so writing empty is correct for them.
        if (skipPersistIfEmpty && items.Count == 0)
        {
            Report($"  {Path.GetFileName(path)}: ARG returned 0 items; not persisting so next call retries.");
            return;
        }

        await WriteCacheFileRawAsync(path, ttl, items, ct).ConfigureAwait(false);
    }

    private async Task<List<JsonElement>> QueryArgAsync(
        string kql,
        string tenantId,
        IReadOnlyList<string>? subscriptionIds,
        CancellationToken ct)
    {
        var token = await GetArmTokenAsync(tenantId, ct).ConfigureAwait(false);
        var results = new List<JsonElement>();
        string? skipToken = null;
        int page = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            page++;

            // Body shape mirrors what `az graph query` sends: subscriptions
            // array scopes the query (without it, ARG returns cross-tenant
            // and pagination can run away — observed 692k+ rows for a 15k
            // resource tenant). $skipToken is the server's cursor for the
            // next page and MUST NOT be sent on the first request.
            //
            // Uses Dictionary<string, object> (not anonymous types) because
            // System.Text.Json's default behavior serializes `object` by its
            // STATIC type — anonymous properties on a variable typed
            // `object` come out empty. Dictionaries serialize by contents.
            var options = new Dictionary<string, object>
            {
                ["resultFormat"] = "objectArray",
                ["$top"] = ArgPageSize,
                ["allowPartialScopes"] = false,
            };
            if (skipToken is not null) options["$skipToken"] = skipToken;

            var payload = new Dictionary<string, object>
            {
                ["query"] = kql,
                ["options"] = options,
            };
            if (subscriptionIds is { Count: > 0 }) payload["subscriptions"] = subscriptionIds;

            var body = JsonSerializer.Serialize(payload);


            var pageSw = System.Diagnostics.Stopwatch.StartNew();
            using var req = new HttpRequestMessage(HttpMethod.Post, ArgQueryUri)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            int pageRows = 0;
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in data.EnumerateArray())
                {
                    results.Add(row.Clone());
                    pageRows++;
                }
            }

            skipToken = doc.RootElement.TryGetProperty("$skipToken", out var st) && st.ValueKind == JsonValueKind.String
                ? st.GetString()
                : null;
            Report($"    page {page}: {pageRows} rows in {pageSw.Elapsed.TotalSeconds:F1}s (total {results.Count}){(skipToken is null ? " [end]" : "")}");
            if (string.IsNullOrEmpty(skipToken)) break;
        }

        return results;
    }

    private async Task<string> GetArmTokenAsync(string? tenantId, CancellationToken ct)
    {
        var result = await _authService.AcquireTokenSilentlyAsync(
            scopes: new[] { ArmScope },
            tenantId: tenantId,
            promptOnError: false,
            cancellationToken: ct).ConfigureAwait(false);
        return result.AccessToken;
    }

    private static async Task WriteCacheFileAsync<T>(string path, TimeSpan ttl, IReadOnlyList<T> items, CancellationToken ct)
    {
        var envelope = new
        {
            schemaVersion = SchemaVersion,
            cachedAt = DateTime.UtcNow,
            ttlSeconds = (int)ttl.TotalSeconds,
            itemCount = items.Count,
            items,
        };
        var payload = JsonSerializer.Serialize(envelope, Json);
        await AtomicWriteAsync(path, payload, ct).ConfigureAwait(false);
    }

    private static async Task WriteCacheFileRawAsync(string path, TimeSpan ttl, IReadOnlyList<JsonElement> items, CancellationToken ct)
    {
        // Build the envelope by hand — the items are already JsonElement, so
        // serializing them via the generic path would re-serialize each into
        // a nested string. Just stitch the JSON together.
        var sb = new StringBuilder(items.Count * 256);
        sb.Append('{');
        sb.Append("\"schemaVersion\":").Append(SchemaVersion).Append(',');
        sb.Append("\"cachedAt\":\"").Append(DateTime.UtcNow.ToString("O")).Append("\",");
        sb.Append("\"ttlSeconds\":").Append((int)ttl.TotalSeconds).Append(',');
        sb.Append("\"itemCount\":").Append(items.Count).Append(',');
        sb.Append("\"items\":[");
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(items[i].GetRawText());
        }
        sb.Append("]}");
        await AtomicWriteAsync(path, sb.ToString(), ct).ConfigureAwait(false);
    }

    private static async Task AtomicWriteAsync(string path, string content, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, content, ct).ConfigureAwait(false);
        // File.Move with overwrite: atomic on the same volume (which ~/.azclips always is).
        File.Move(tmp, path, overwrite: true);
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }
}
