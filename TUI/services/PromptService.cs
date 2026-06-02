using System.Net.Http.Headers;
using System.Text.Json;
using Core.Tui.Azure;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Tui.Services;

/// <summary>
/// Builds the shell prompt string based on current mode, VFS path, and Azure context.
/// </summary>
class PromptService
{
    private static readonly string CirrusDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
    private static readonly string ContextFilePath = Path.Combine(CirrusDir, "context.json");

    // Cached ARM-resolved names
    private string? _cachedTenantId;
    private string? _cachedSubId;
    private string? _cachedTenantName;
    private string? _cachedSubName;

    /// <summary>
    /// Build the prompt string for the current state.
    /// </summary>
    public string BuildPrompt(ShellMode mode, string localCwd, bool vfsReady, AzureVfs vfs)
    {
        if (mode == ShellMode.Local)
            return BuildLocalPrompt(localCwd);

        if (vfsReady)
            return vfs.CurrentPath == "/" ? "/ > " : $"{vfs.CurrentPath} > ";

        return BuildFallbackAzurePrompt();
    }

    /// <summary>
    /// Get the current git branch name.
    /// </summary>
    public static string GetGitBranch(string? workingDirectory = null)
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "branch --show-current",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? "",
                }
            };
            process.Start();
            var branch = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(2000);
            return branch;
        }
        catch
        {
            return "";
        }
    }

    private static string BuildLocalPrompt(string localCwd)
    {
        var cwd = localCwd;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (cwd.StartsWith(home, StringComparison.OrdinalIgnoreCase))
            cwd = "~" + cwd[home.Length..];
        return $"{cwd} > ";
    }

    private string BuildFallbackAzurePrompt()
    {
        try
        {
            if (!File.Exists(ContextFilePath)) return "/ > ";

            var json = File.ReadAllText(ContextFilePath);
            var doc = JsonDocument.Parse(json);
            var tenantId = doc.RootElement.TryGetProperty("TenantId", out var t) ? t.GetString() : null;
            var subId = doc.RootElement.TryGetProperty("SubscriptionId", out var s) ? s.GetString() : null;

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(subId))
                return "/ > ";

            if (tenantId == _cachedTenantId && subId == _cachedSubId
                && _cachedTenantName != null && _cachedSubName != null)
                return $"/{_cachedTenantName}/{_cachedSubName} > ";

            var (tenantName, subName) = ResolveNamesFromArm(tenantId, subId);
            _cachedTenantId = tenantId;
            _cachedSubId = subId;
            _cachedTenantName = tenantName ?? tenantId[..Math.Min(8, tenantId.Length)];
            _cachedSubName = subName ?? subId[..Math.Min(8, subId.Length)];
            return $"/{_cachedTenantName}/{_cachedSubName} > ";
        }
        catch
        {
            return "/ > ";
        }
    }

    private static (string? tenantName, string? subName) ResolveNamesFromArm(string tenantId, string subId)
    {
        try
        {
            var app = PublicClientApplicationBuilder
                .Create("1950a258-227b-4e31-a9cf-717495945fc2")
                .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
                .WithRedirectUri("http://localhost")
                .Build();

            var cacheFile = Path.Combine(CirrusDir, "MSAL.cache.plaintext");
            if (!File.Exists(cacheFile)) return (null, null);

            var storageProps = new StorageCreationPropertiesBuilder("MSAL.cache.plaintext", CirrusDir)
                .WithUnprotectedFile()
                .Build();
            var cacheHelper = MsalCacheHelper.CreateAsync(storageProps).GetAwaiter().GetResult();
            cacheHelper.RegisterCache(app.UserTokenCache);

            var accounts = app.GetAccountsAsync().GetAwaiter().GetResult();
            var account = accounts.FirstOrDefault();
            if (account == null) return (null, null);

            var result = app.AcquireTokenSilent(
                ["https://management.core.windows.net//.default"], account)
                .WithForceRefresh(false)
                .ExecuteAsync().GetAwaiter().GetResult();

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            var armBase = "https://management.azure.com";

            string? subName = null;
            string? tenantName = null;

            try
            {
                var subJson = http.GetStringAsync($"{armBase}/subscriptions/{subId}?api-version=2022-12-01").GetAwaiter().GetResult();
                using var subDoc = JsonDocument.Parse(subJson);
                if (subDoc.RootElement.TryGetProperty("displayName", out var sn))
                    subName = sn.GetString();
            }
            catch { }

            try
            {
                var tenantsJson = http.GetStringAsync($"{armBase}/tenants?api-version=2022-12-01").GetAwaiter().GetResult();
                using var tenantsDoc = JsonDocument.Parse(tenantsJson);
                if (tenantsDoc.RootElement.TryGetProperty("value", out var tenants))
                {
                    foreach (var tenant in tenants.EnumerateArray())
                    {
                        if (tenant.TryGetProperty("tenantId", out var tid) && tid.GetString() == tenantId)
                        {
                            if (tenant.TryGetProperty("displayName", out var tn))
                                tenantName = tn.GetString();
                            break;
                        }
                    }
                }
            }
            catch { }

            return (tenantName, subName);
        }
        catch
        {
            return (null, null);
        }
    }
}
