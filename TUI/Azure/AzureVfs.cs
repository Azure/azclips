using System.Text.Json;
using Core.Abstractions;

namespace Core.Tui.Azure;

/// <summary>
/// Azure Virtual File System — organizes Azure resources into a navigable tree:
///   / (root)
///   └─ {tenantId} (tenant — from Core.Authentication context)
///      └─ {subscriptionName} (subscription — from ARG)
///         └─ {resourceGroupName} (resourceGroup — from ARG)
///            └─ {resourceName} [{resourceType}] (resource — from ARG)
///
/// Tenant comes from the active profile (IProfileService).
/// Subscriptions, resource groups, and resources all use Azure Resource Graph.
/// </summary>
public class AzureVfs
{
    private readonly ArmClient _arm;
    private readonly IProfileService _profileService;
    private AzureResourceTree? _tree;
    private Task? _treePreloadTask;

    private string _currentPath = "/";
    private string? _tenantId;
    private string _tenantDisplay = "";

    public string CurrentPath => _currentPath;
    public string? TenantId => _tenantId;
    public bool IsAuthenticated => _arm.IsAuthenticated;

    public AzureVfs(ArmClient arm, IProfileService profileService)
    {
        _arm = arm;
        _profileService = profileService;
    }

    /// <summary>
    /// Try to authenticate silently using existing MSAL cache.
    /// </summary>
    public async Task<bool> TryAuthenticateAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var context = await _profileService.GetActiveProfileAsync();
        Console.Error.WriteLine($"[startup]     GetActiveProfileAsync: {sw.ElapsedMilliseconds}ms");
        var tenantId = context?.TenantId;
        sw.Restart();
        var ok = await _arm.TryAuthenticateAsync(tenantId);
        Console.Error.WriteLine($"[startup]     ArmClient.TryAuthenticateAsync: {sw.ElapsedMilliseconds}ms");
        if (ok && tenantId != null)
        {
            _tenantId = tenantId;
        }
        return ok;
    }

    /// <summary>
    /// Set the initial VFS path from auth context (tenantId + subscriptionId).
    /// Resolves tenant display name and subscription name in parallel using lightweight queries.
    /// Also kicks off async background preload of the full resource tree.
    /// </summary>
    public async Task SetInitialPathAsync()
    {
        var context = await _profileService.GetActiveProfileAsync();
        var tenantId = context?.TenantId;
        var subscriptionId = context?.SubscriptionId;
        if (string.IsNullOrEmpty(tenantId)) return;

        _tenantId = tenantId;

        // Resolve tenant display name and subscription name in parallel
        var tenantNameTask = _arm.GetTenantDisplayNameAsync(tenantId);
        var subNameTask = !string.IsNullOrEmpty(subscriptionId)
            ? _arm.FetchSubscriptionDisplayNameAsync(subscriptionId)
            : Task.FromResult<string?>(null);

        await Task.WhenAll(tenantNameTask, subNameTask);

        _tenantDisplay = tenantNameTask.Result
            ?? tenantId[..Math.Min(8, tenantId.Length)];

        if (!string.IsNullOrEmpty(subscriptionId))
        {
            var subName = subNameTask.Result ?? subscriptionId;
            _currentPath = $"/{_tenantDisplay}/{subName}";
        }
        else
        {
            _currentPath = $"/{_tenantDisplay}";
        }

        // Kick off async preload of the full resource tree (non-blocking)
        _treePreloadTask = Task.Run(async () =>
        {
            try { _tree = await _arm.FetchResourceTreeAsync(); }
            catch { /* preload failure is non-fatal; will retry on demand */ }
        });
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

    public void Refresh()
    {
        _tree = null;
    }

    // ── Completion ──

    /// <summary>
    /// Get path completion suggestions for the given partial input.
    /// </summary>
    public async Task<List<VfsEntry>> GetCompletionsAsync(string partial)
    {
        // Empty partial → list all children of current directory
        if (string.IsNullOrEmpty(partial))
        {
            return await LsAsync();
        }

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

        // Prefix matches first, then substring matches (deduped)
        var prefix = candidates
            .Where(c => c.Name.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase));

        var contains = candidates
            .Where(c => !c.Name.StartsWith(lastPart, StringComparison.OrdinalIgnoreCase)
                     && c.Name.Contains(lastPart, StringComparison.OrdinalIgnoreCase));

        return prefix.Concat(contains).ToList();
    }

    // ── Internal data fetching (single query, cached in memory) ──

    private async Task<AzureResourceTree> GetTreeAsync()
    {
        if (_tree != null) return _tree;

        // If a preload is in progress, await it instead of starting a new fetch
        if (_treePreloadTask != null)
        {
            await _treePreloadTask;
            if (_tree != null) return _tree;
        }

        _tree = await _arm.FetchResourceTreeAsync();
        return _tree;
    }

    private async Task<List<AzureSubscription>> GetSubscriptionsAsync()
        => (await GetTreeAsync()).Subscriptions;

    private async Task<List<AzureResourceGroup>> GetResourceGroupsAsync(string subscriptionId)
        => (await GetTreeAsync()).GetResourceGroups(subscriptionId);

    private async Task<List<AzureResource>> GetResourcesAsync(string subscriptionId, string resourceGroupName)
        => (await GetTreeAsync()).GetResources(subscriptionId, resourceGroupName);

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

        // Validate tenant (must match the active profile's tenant)
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
