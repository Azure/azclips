using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Tui.Azure;

/// <summary>
/// Lightweight Azure client using MSAL tokens.
/// Uses Azure Resource Graph for subscriptions, resource groups, and resources.
/// Tenant info comes from context.json (written by Core.Authentication).
/// </summary>
public class ArmClient : IDisposable
{
    private const string ArmBase = "https://management.azure.com";
    private const string ArgEndpoint = $"{ArmBase}/providers/Microsoft.ResourceGraph/resources?api-version=2022-10-01";
    private const string ArmScope = "https://management.core.windows.net//.default";
    private const string ClientId = "1950a258-227b-4e31-a9cf-717495945fc2";

    private readonly HttpClient _http = new();
    private IPublicClientApplication? _app;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry;

    public bool IsAuthenticated => _cachedToken != null && _tokenExpiry > DateTimeOffset.UtcNow;

    /// <summary>
    /// Acquire a token silently from the shared MSAL cache.
    /// Returns true if authentication succeeded.
    /// </summary>
    public async Task<bool> TryAuthenticateAsync(string? tenantId = null)
    {
        try
        {
            var token = await AcquireTokenAsync(tenantId);
            return token != null;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> AcquireTokenAsync(string? tenantId = null)
    {
        if (_cachedToken != null && _tokenExpiry > DateTimeOffset.UtcNow.AddMinutes(1))
            return _cachedToken;

        _app ??= await BuildAppAsync(tenantId);

        var accounts = await _app.GetAccountsAsync();
        var account = accounts.FirstOrDefault();
        if (account == null) return null;

        try
        {
            var result = await _app.AcquireTokenSilent([ArmScope], account).ExecuteAsync();
            _cachedToken = result.AccessToken;
            _tokenExpiry = result.ExpiresOn;
            return _cachedToken;
        }
        catch (MsalUiRequiredException)
        {
            return null;
        }
    }

    private static async Task<IPublicClientApplication> BuildAppAsync(string? tenantId)
    {
        var app = PublicClientApplicationBuilder.Create(ClientId)
            .WithRedirectUri("http://localhost")
            .WithTenantId(tenantId ?? "organizations")
            .Build();

        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
        var props = new StorageCreationPropertiesBuilder("MSAL.cache.plaintext", cacheDir)
            .WithUnprotectedFile()
            .Build();
        var helper = await MsalCacheHelper.CreateAsync(props);
        helper.RegisterCache(app.UserTokenCache);
        return app;
    }

    // ── Azure Resource Graph queries ──

    /// <summary>
    /// Get all subscriptions via Resource Graph.
    /// </summary>
    public async Task<List<AzureSubscription>> GetSubscriptionsAsync()
    {
        const string kql = "ResourceContainers | where type == 'microsoft.resources/subscriptions' | project subscriptionId, name, properties.state, tenantId";
        var rows = await QueryArgAsync(kql, subscriptionIds: null);
        return rows.Select(cols => new AzureSubscription
        {
            SubscriptionId = cols[0].GetString() ?? "",
            DisplayName = cols[1].GetString(),
            State = cols[2].GetString(),
            TenantId = cols[3].GetString()
        }).ToList();
    }

    /// <summary>
    /// Get resource groups for a subscription via Resource Graph.
    /// </summary>
    public async Task<List<AzureResourceGroup>> GetResourceGroupsAsync(string subscriptionId)
    {
        const string kql = "ResourceContainers | where type == 'microsoft.resources/subscriptions/resourcegroups' | project name, location, subscriptionId, properties.provisioningState";
        var rows = await QueryArgAsync(kql, [subscriptionId]);
        return rows.Select(cols => new AzureResourceGroup
        {
            Name = cols[0].GetString() ?? "",
            Location = cols[1].GetString() ?? "",
        }).ToList();
    }

    /// <summary>
    /// Get resources via Resource Graph, optionally filtered by resource group.
    /// </summary>
    public async Task<List<AzureResource>> GetResourcesAsync(string subscriptionId, string? resourceGroup = null)
    {
        var kql = resourceGroup != null
            ? $"Resources | where resourceGroup =~ '{resourceGroup}' | project name, type, location, resourceGroup, subscriptionId"
            : "Resources | project name, type, location, resourceGroup, subscriptionId";
        var rows = await QueryArgAsync(kql, [subscriptionId]);
        return rows.Select(cols => new AzureResource
        {
            Name = cols[0].GetString() ?? "",
            Type = cols[1].GetString() ?? "",
            Location = cols[2].GetString(),
        }).ToList();
    }

    /// <summary>
    /// Execute an ARG query and return raw rows.
    /// If subscriptionIds is null, queries across all accessible subscriptions.
    /// </summary>
    private async Task<List<List<JsonElement>>> QueryArgAsync(string kql, List<string>? subscriptionIds)
    {
        var token = await AcquireTokenAsync();
        if (token == null) throw new InvalidOperationException("Not authenticated. Run 'login' first.");

        var results = new List<List<JsonElement>>();
        string? skipToken = null;

        do
        {
            var body = new Dictionary<string, object> { ["query"] = kql };
            if (subscriptionIds != null)
                body["subscriptions"] = subscriptionIds;
            if (skipToken != null)
                body["options"] = new Dictionary<string, string> { ["$skipToken"] = skipToken };

            var json = JsonSerializer.Serialize(body);
            using var request = new HttpRequestMessage(HttpMethod.Post, ArgEndpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);

            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("rows", out var rows))
            {
                foreach (var row in rows.EnumerateArray())
                    results.Add(row.EnumerateArray().ToList());
            }

            skipToken = doc.RootElement.TryGetProperty("$skipToken", out var st) ? st.GetString() : null;
        } while (skipToken != null);

        return results;
    }

    public void Dispose()
    {
        _http.Dispose();
        GC.SuppressFinalize(this);
    }
}
