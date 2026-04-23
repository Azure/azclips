using System.Net.Http.Json;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Tui.Azure;

/// <summary>
/// Lightweight ARM REST client using MSAL tokens.
/// Reuses the same MSAL cache as Core.Authentication so `login` tokens work here.
/// </summary>
public class ArmClient : IDisposable
{
    private const string ArmBase = "https://management.azure.com";
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

    // ── ARM REST methods ──

    public async Task<List<AzureTenant>> GetTenantsAsync()
    {
        return await GetArmListAsync<AzureTenant>("/tenants?api-version=2022-12-01");
    }

    public async Task<List<AzureSubscription>> GetSubscriptionsAsync()
    {
        return await GetArmListAsync<AzureSubscription>("/subscriptions?api-version=2022-12-01");
    }

    public async Task<List<AzureResourceGroup>> GetResourceGroupsAsync(string subscriptionId)
    {
        return await GetArmListAsync<AzureResourceGroup>(
            $"/subscriptions/{subscriptionId}/resourcegroups?api-version=2024-03-01");
    }

    public async Task<List<AzureResource>> GetResourcesAsync(string subscriptionId, string resourceGroupName)
    {
        return await GetArmListAsync<AzureResource>(
            $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/resources?api-version=2024-03-01");
    }

    private async Task<List<T>> GetArmListAsync<T>(string path)
    {
        var token = await AcquireTokenAsync();
        if (token == null) throw new InvalidOperationException("Not authenticated. Run 'login' first.");

        var results = new List<T>();
        string? url = $"{ArmBase}{path}";

        while (url != null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var listResult = await response.Content.ReadFromJsonAsync<ArmListResponse<T>>();
            if (listResult?.Value != null)
                results.AddRange(listResult.Value);

            url = listResult?.NextLink;
        }

        return results;
    }

    public void Dispose()
    {
        _http.Dispose();
        GC.SuppressFinalize(this);
    }
}
