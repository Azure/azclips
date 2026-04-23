namespace Core.Tui.Azure;

/// <summary>
/// Azure Virtual File System — organizes Azure resources into a navigable tree:
///   / (root)
///   └─ {tenantName} (tenant)
///      └─ {subscriptionName} (subscription)
///         └─ {resourceGroupName} (resourceGroup)
///            └─ {resourceName} [{resourceType}] (resource)
///
/// Mirrors the TypeScript AzureVFS from shell/packages/core/src/tools/azure/vfs/.
/// Uses lazy loading with TTL-based caching.
/// </summary>
public class AzureVfs
{
    private readonly ArmClient _arm;
    private readonly AzureCache<List<AzureTenant>> _tenantCache = new();
    private readonly AzureCache<List<AzureSubscription>> _subscriptionCache = new();
    private readonly AzureCache<List<AzureResourceGroup>> _resourceGroupCache = new();
    private readonly AzureCache<List<AzureResource>> _resourceCache = new();

    private string _currentPath = "/";

    public string CurrentPath => _currentPath;
    public bool IsAuthenticated => _arm.IsAuthenticated;

    public AzureVfs(ArmClient arm)
    {
        _arm = arm;
    }

    /// <summary>
    /// Try to authenticate silently using existing MSAL cache.
    /// </summary>
    public Task<bool> TryAuthenticateAsync(string? tenantId = null)
        => _arm.TryAuthenticateAsync(tenantId);

    /// <summary>
    /// Set the initial VFS path from a tenantId + subscriptionId (e.g. from context.json).
    /// Resolves IDs to display names so the path uses human-readable segments.
    /// </summary>
    public async Task SetInitialPathAsync(string? tenantId, string? subscriptionId)
    {
        if (string.IsNullOrEmpty(tenantId)) return;

        // Resolve tenant display name
        var tenants = await GetTenantsAsync();
        var tenant = tenants.FirstOrDefault(t =>
            t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));
        if (tenant == null) return;
        var tenantName = TenantDisplayName(tenant);

        if (string.IsNullOrEmpty(subscriptionId))
        {
            _currentPath = $"/{tenantName}";
            return;
        }

        // Resolve subscription display name
        var subs = await GetSubscriptionsAsync();
        var sub = subs.FirstOrDefault(s =>
            s.SubscriptionId.Equals(subscriptionId, StringComparison.OrdinalIgnoreCase));
        var subName = sub?.DisplayName ?? subscriptionId;

        _currentPath = $"/{tenantName}/{subName}";
    }

    // ── Navigation ──

    /// <summary>
    /// Change current directory. Supports absolute, relative, "..", and "~" paths.
    /// Returns error message on failure, null on success.
    /// </summary>
    public async Task<string?> CdAsync(string path)
    {
        var resolved = ResolvePath(path);
        var parts = ParsePath(resolved);

        // Validate the path exists
        var error = await ValidatePathAsync(parts);
        if (error != null) return error;

        _currentPath = resolved;
        return null;
    }

    /// <summary>
    /// List children of the current (or specified) path.
    /// Returns list of (name, type, detail) tuples.
    /// </summary>
    public async Task<List<VfsEntry>> LsAsync(string? path = null)
    {
        var target = path != null ? ResolvePath(path) : _currentPath;
        var parts = ParsePath(target);

        return parts.Length switch
        {
            0 => await ListTenantsAsync(),
            1 => await ListSubscriptionsAsync(parts[0]),
            2 => await ListResourceGroupsAsync(parts[1]),
            3 => await ListResourcesAsync(parts[1], parts[2]),
            _ => []
        };
    }

    /// <summary>
    /// Get a tree representation of the full VFS from root, up to specified depth.
    /// </summary>
    public async Task<List<string>> TreeAsync(int maxDepth = 3)
    {
        var lines = new List<string> { "/" };
        if (maxDepth < 1) return lines;

        var tenants = await GetTenantsAsync();
        for (int t = 0; t < tenants.Count; t++)
        {
            var tenant = tenants[t];
            var tPrefix = t == tenants.Count - 1 ? "└─ " : "├─ ";
            var tCont = t == tenants.Count - 1 ? "   " : "│  ";
            lines.Add($"{tPrefix}🏢 {TenantDisplayName(tenant)}");

            if (maxDepth < 2) continue;
            var subs = await GetSubscriptionsAsync();
            for (int s = 0; s < subs.Count; s++)
            {
                var sub = subs[s];
                var sPrefix = s == subs.Count - 1 ? "└─ " : "├─ ";
                var sCont = s == subs.Count - 1 ? "   " : "│  ";
                lines.Add($"{tCont}{sPrefix}📋 {sub.DisplayName ?? sub.SubscriptionId}");

                if (maxDepth < 3) continue;
                var rgs = await GetResourceGroupsAsync(sub.SubscriptionId);
                for (int r = 0; r < rgs.Count; r++)
                {
                    var rg = rgs[r];
                    var rPrefix = r == rgs.Count - 1 ? "└─ " : "├─ ";
                    lines.Add($"{tCont}{sCont}{rPrefix}📁 {rg.Name} ({rg.Location})");
                }
            }
        }

        return lines;
    }

    /// <summary>
    /// Get the Azure Resource Manager resource ID for the current path.
    /// </summary>
    public string GetResourceId()
    {
        var parts = ParsePath(_currentPath);
        return parts.Length switch
        {
            0 => "/",
            1 => $"/tenants/{parts[0]}",
            2 => $"/subscriptions/{parts[1]}",
            3 => $"/subscriptions/{parts[1]}/resourceGroups/{parts[2]}",
            _ => _currentPath
        };
    }

    // ── Cache refresh ──

    public enum RefreshScope { All, Tenants, Subscriptions, ResourceGroups, Resources }

    public void Refresh(RefreshScope scope, string? subscriptionId = null, string? resourceGroupName = null)
    {
        switch (scope)
        {
            case RefreshScope.All:
                _tenantCache.Clear();
                _subscriptionCache.Clear();
                _resourceGroupCache.Clear();
                _resourceCache.Clear();
                break;
            case RefreshScope.Tenants:
                _tenantCache.Clear();
                break;
            case RefreshScope.Subscriptions:
                _subscriptionCache.Clear();
                break;
            case RefreshScope.ResourceGroups:
                if (subscriptionId != null) _resourceGroupCache.Invalidate(subscriptionId);
                else _resourceGroupCache.Clear();
                break;
            case RefreshScope.Resources:
                if (subscriptionId != null && resourceGroupName != null)
                    _resourceCache.Invalidate($"{subscriptionId}/{resourceGroupName}");
                else if (subscriptionId != null)
                {
                    foreach (var key in _resourceCache.Keys.Where(k => k.StartsWith($"{subscriptionId}/")))
                        _resourceCache.Invalidate(key);
                }
                else
                    _resourceCache.Clear();
                break;
        }
    }

    // ── Completion ──

    /// <summary>
    /// Get path completion suggestions for the given partial input.
    /// </summary>
    public async Task<List<string>> GetCompletionsAsync(string partial)
    {
        var resolved = ResolvePath(partial);
        var parts = ParsePath(resolved);
        var lastPart = parts.Length > 0 ? parts[^1] : "";

        // Determine parent path parts (everything except the last segment being completed)
        var parentParts = parts.Length > 1 ? parts[..^1] : Array.Empty<string>();

        List<VfsEntry> candidates;
        try
        {
            candidates = parentParts.Length switch
            {
                0 => await ListTenantsAsync(),
                1 => await ListSubscriptionsAsync(parentParts[0]),
                2 => await ListResourceGroupsAsync(parentParts[1]),
                3 => await ListResourcesAsync(parentParts[1], parentParts[2]),
                _ => []
            };
        }
        catch
        {
            return [];
        }

        return candidates
            .Where(c => c.Name.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Name)
            .ToList();
    }

    // ── Internal data fetching (cached) ──

    private Task<List<AzureTenant>> GetTenantsAsync()
        => _tenantCache.GetOrFetchAsync("tenants", _arm.GetTenantsAsync);

    private Task<List<AzureSubscription>> GetSubscriptionsAsync()
        => _subscriptionCache.GetOrFetchAsync("subs", _arm.GetSubscriptionsAsync);

    private Task<List<AzureResourceGroup>> GetResourceGroupsAsync(string subscriptionId)
        => _resourceGroupCache.GetOrFetchAsync(subscriptionId,
            () => _arm.GetResourceGroupsAsync(subscriptionId));

    private Task<List<AzureResource>> GetResourcesAsync(string subscriptionId, string resourceGroupName)
        => _resourceCache.GetOrFetchAsync($"{subscriptionId}/{resourceGroupName}",
            () => _arm.GetResourcesAsync(subscriptionId, resourceGroupName));

    // ── List helpers (return VfsEntry) ──

    private async Task<List<VfsEntry>> ListTenantsAsync()
    {
        var tenants = await GetTenantsAsync();
        return tenants.Select(t => new VfsEntry(
            TenantDisplayName(t),
            VfsEntryKind.Directory,
            t.DefaultDomain ?? t.TenantId
        )).ToList();
    }

    private async Task<List<VfsEntry>> ListSubscriptionsAsync(string tenantNameOrId)
    {
        var subs = await GetSubscriptionsAsync();
        // Filter by tenant if we can (subscriptions have tenantId)
        var tenants = await GetTenantsAsync();
        var tenant = ResolveTenant(tenants, tenantNameOrId);
        if (tenant != null)
            subs = subs.Where(s => s.TenantId == tenant.TenantId).ToList();

        return subs.Select(s => new VfsEntry(
            s.DisplayName ?? s.SubscriptionId,
            VfsEntryKind.Directory,
            s.SubscriptionId
        )).ToList();
    }

    private async Task<List<VfsEntry>> ListResourceGroupsAsync(string subscriptionNameOrId)
    {
        var subId = await ResolveSubscriptionIdAsync(subscriptionNameOrId);
        if (subId == null) return [];

        var rgs = await GetResourceGroupsAsync(subId);
        return rgs.Select(rg => new VfsEntry(
            rg.Name,
            VfsEntryKind.Directory,
            rg.Location
        )).ToList();
    }

    private async Task<List<VfsEntry>> ListResourcesAsync(string subscriptionNameOrId, string rgName)
    {
        var subId = await ResolveSubscriptionIdAsync(subscriptionNameOrId);
        if (subId == null) return [];

        var resources = await GetResourcesAsync(subId, rgName);
        return resources.Select(r => new VfsEntry(
            r.Name,
            VfsEntryKind.File,
            r.Type.Split('/').LastOrDefault() ?? r.Type
        )).ToList();
    }

    // ── Path resolution ──

    private string ResolvePath(string input)
    {
        input = input.Trim();
        if (input == "~" || input == "") return "/";
        if (input.StartsWith("~/")) input = "/" + input[2..];
        if (!input.StartsWith('/'))
            input = _currentPath.TrimEnd('/') + "/" + input;

        // Resolve ".."
        var segments = input.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        var resolved = new List<string>();
        foreach (var seg in segments)
        {
            if (seg == "..") { if (resolved.Count > 0) resolved.RemoveAt(resolved.Count - 1); }
            else if (seg != ".") resolved.Add(seg);
        }

        return "/" + string.Join("/", resolved);
    }

    private static string[] ParsePath(string path)
        => path.Split('/', StringSplitOptions.RemoveEmptyEntries);

    private async Task<string?> ValidatePathAsync(string[] parts)
    {
        if (parts.Length == 0) return null; // root always valid

        // Validate tenant
        var tenants = await GetTenantsAsync();
        if (ResolveTenant(tenants, parts[0]) == null)
            return $"Tenant not found: {parts[0]}";
        if (parts.Length == 1) return null;

        // Validate subscription
        var subId = await ResolveSubscriptionIdAsync(parts[1]);
        if (subId == null)
            return $"Subscription not found: {parts[1]}";
        if (parts.Length == 2) return null;

        // Validate resource group
        var rgs = await GetResourceGroupsAsync(subId);
        if (!rgs.Any(rg => rg.Name.Equals(parts[2], StringComparison.OrdinalIgnoreCase)))
            return $"Resource group not found: {parts[2]}";

        return null; // deeper paths allowed
    }

    private async Task<string?> ResolveSubscriptionIdAsync(string nameOrId)
    {
        var subs = await GetSubscriptionsAsync();
        var match = subs.FirstOrDefault(s =>
            s.SubscriptionId.Equals(nameOrId, StringComparison.OrdinalIgnoreCase) ||
            (s.DisplayName?.Equals(nameOrId, StringComparison.OrdinalIgnoreCase) ?? false));
        return match?.SubscriptionId;
    }

    private static AzureTenant? ResolveTenant(List<AzureTenant> tenants, string nameOrId)
        => tenants.FirstOrDefault(t =>
            t.TenantId.Equals(nameOrId, StringComparison.OrdinalIgnoreCase) ||
            (t.DisplayName?.Equals(nameOrId, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (t.DefaultDomain?.Equals(nameOrId, StringComparison.OrdinalIgnoreCase) ?? false));

    private static string TenantDisplayName(AzureTenant t)
        => t.DisplayName ?? t.DefaultDomain ?? t.TenantId;
}

// ── VFS entry types ──

public enum VfsEntryKind { Directory, File }

public record VfsEntry(string Name, VfsEntryKind Kind, string Detail);
