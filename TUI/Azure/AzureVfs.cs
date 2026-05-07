using System.Text.Json;

namespace Core.Tui.Azure;

/// <summary>
/// Azure Virtual File System — organizes Azure resources into a navigable tree:
///   / (root)
///   └─ {tenantId} (tenant — from context.json)
///      └─ {subscriptionName} (subscription — from ARG)
///         └─ {resourceGroupName} (resourceGroup — from ARG)
///            └─ {resourceName} [{resourceType}] (resource — from ARG)
///
/// Tenant comes from context.json (written by Core.Authentication).
/// Subscriptions, resource groups, and resources all use Azure Resource Graph.
/// </summary>
public class AzureVfs
{
    private static readonly string ContextFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus", "context.json");

    private readonly ArmClient _arm;
    private readonly AzureCache<List<AzureSubscription>> _subscriptionCache = new();
    private readonly AzureCache<List<AzureResourceGroup>> _resourceGroupCache = new();
    private readonly AzureCache<List<AzureResource>> _resourceCache = new();

    private string _currentPath = "/";
    private string? _tenantId;
    private string _tenantDisplay = "";

    public string CurrentPath => _currentPath;
    public string? TenantId => _tenantId;
    public bool IsAuthenticated => _arm.IsAuthenticated;

    public AzureVfs(ArmClient arm)
    {
        _arm = arm;
    }

    /// <summary>
    /// Try to authenticate silently using existing MSAL cache.
    /// </summary>
    public async Task<bool> TryAuthenticateAsync()
    {
        var (tenantId, _) = LoadContext();
        var ok = await _arm.TryAuthenticateAsync(tenantId);
        if (ok && tenantId != null)
        {
            _tenantId = tenantId;
            _tenantDisplay = tenantId[..Math.Min(8, tenantId.Length)];
        }
        return ok;
    }

    /// <summary>
    /// Set the initial VFS path from context.json (tenantId + subscriptionId).
    /// </summary>
    public async Task SetInitialPathAsync()
    {
        var (tenantId, subscriptionId) = LoadContext();
        if (string.IsNullOrEmpty(tenantId)) return;

        _tenantId = tenantId;
        _tenantDisplay = tenantId[..Math.Min(8, tenantId.Length)];

        if (string.IsNullOrEmpty(subscriptionId))
        {
            _currentPath = $"/{_tenantDisplay}";
            return;
        }

        // Resolve subscription display name via ARG
        var subs = await GetSubscriptionsAsync();
        var sub = subs.FirstOrDefault(s =>
            s.SubscriptionId.Equals(subscriptionId, StringComparison.OrdinalIgnoreCase));
        var subName = sub?.DisplayName ?? subscriptionId;

        _currentPath = $"/{_tenantDisplay}/{subName}";
    }

    /// <summary>
    /// Read tenant/subscription from context.json (written by Core.Authentication).
    /// </summary>
    private static (string? tenantId, string? subscriptionId) LoadContext()
    {
        try
        {
            if (!File.Exists(ContextFilePath)) return (null, null);
            var json = File.ReadAllText(ContextFilePath);
            using var doc = JsonDocument.Parse(json);
            var tenantId = doc.RootElement.TryGetProperty("TenantId", out var t) ? t.GetString() : null;
            var subId = doc.RootElement.TryGetProperty("SubscriptionId", out var s) ? s.GetString() : null;
            return (tenantId, subId);
        }
        catch { return (null, null); }
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
            0 => ListTenant(),
            1 => await ListSubscriptionsAsync(),
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
        if (maxDepth < 1 || _tenantId == null) return lines;

        lines.Add($"└─ ◆ {_tenantDisplay}");

        if (maxDepth < 2) return lines;
        var subs = await GetSubscriptionsAsync();
        for (int s = 0; s < subs.Count; s++)
        {
            var sub = subs[s];
            var sPrefix = s == subs.Count - 1 ? "   └─ " : "   ├─ ";
            var sCont = s == subs.Count - 1 ? "      " : "   │  ";
            lines.Add($"{sPrefix}◇ {sub.DisplayName ?? sub.SubscriptionId}");

            if (maxDepth < 3) continue;
            var rgs = await GetResourceGroupsAsync(sub.SubscriptionId);
            for (int r = 0; r < rgs.Count; r++)
            {
                var rg = rgs[r];
                var rPrefix = r == rgs.Count - 1 ? "└─ " : "├─ ";
                lines.Add($"{sCont}{rPrefix}▸ {rg.Name} ({rg.Location})");
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

    /// <summary>
    /// Get the current subscription name from the VFS path (if at subscription level or deeper).
    /// </summary>
    public string? GetCurrentSubscriptionName()
    {
        var parts = ParsePath(_currentPath);
        return parts.Length >= 2 ? parts[1] : null;
    }

    /// <summary>
    /// Get the current context (tenantId, subscriptionId) from the VFS path.
    /// Returns resolved IDs (not display names).
    /// </summary>
    public async Task<(string? tenantId, string? subscriptionId)> GetCurrentContextAsync()
    {
        var parts = ParsePath(_currentPath);
        if (parts.Length < 1) return (null, null);

        var tenantId = _tenantId;
        string? subId = null;
        if (parts.Length >= 2)
            subId = await ResolveSubscriptionIdAsync(parts[1]);

        return (tenantId, subId);
    }

    // ── Cache refresh ──

    public enum RefreshScope { All, Subscriptions, ResourceGroups, Resources }

    public void Refresh(RefreshScope scope, string? subscriptionId = null, string? resourceGroupName = null)
    {
        switch (scope)
        {
            case RefreshScope.All:
                _subscriptionCache.Clear();
                _resourceGroupCache.Clear();
                _resourceCache.Clear();
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
                0 => ListTenant(),
                1 => await ListSubscriptionsAsync(),
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

    // ── Internal data fetching (cached, all via ARG) ──

    private Task<List<AzureSubscription>> GetSubscriptionsAsync()
        => _subscriptionCache.GetOrFetchAsync("subs", _arm.GetSubscriptionsAsync);

    private Task<List<AzureResourceGroup>> GetResourceGroupsAsync(string subscriptionId)
        => _resourceGroupCache.GetOrFetchAsync(subscriptionId,
            () => _arm.GetResourceGroupsAsync(subscriptionId));

    private Task<List<AzureResource>> GetResourcesAsync(string subscriptionId, string resourceGroupName)
        => _resourceCache.GetOrFetchAsync($"{subscriptionId}/{resourceGroupName}",
            () => _arm.GetResourcesAsync(subscriptionId, resourceGroupName));

    // ── List helpers (return VfsEntry) ──

    private List<VfsEntry> ListTenant()
    {
        if (_tenantId == null) return [];
        return [new VfsEntry(_tenantDisplay, VfsEntryKind.Directory, _tenantId)];
    }

    private async Task<List<VfsEntry>> ListSubscriptionsAsync()
    {
        var subs = await GetSubscriptionsAsync();
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

        // Validate tenant (must match current tenant from context.json)
        if (_tenantId == null) return "No tenant configured. Run 'login' first.";
        if (!parts[0].Equals(_tenantDisplay, StringComparison.OrdinalIgnoreCase))
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
}

// ── VFS entry types ──

public enum VfsEntryKind { Directory, File }

public record VfsEntry(string Name, VfsEntryKind Kind, string Detail);
