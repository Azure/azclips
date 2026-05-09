using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Core.Abstractions;

namespace Core.Tui.Azure;

/// <summary>
/// Lightweight Azure client that uses Core.Authentication for tokens.
/// Uses Azure Resource Graph for subscriptions, resource groups, and resources.
/// </summary>
public class ArmClient : IDisposable
{
    private const string ArgEndpoint = "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2022-10-01";

    private readonly IAuthService _authService;
    private readonly HttpClient _http = new();
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry;

    public ArmClient(IAuthService authService)
    {
        _authService = authService;
    }

    public bool IsAuthenticated => _cachedToken != null && _tokenExpiry > DateTimeOffset.UtcNow;

    /// <summary>
    /// Acquire a token silently from the shared MSAL cache via Core.Authentication.
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

        var result = await _authService.AcquireTokenSilentlyAsync(null, tenantId);
        _cachedToken = result.AccessToken;
        _tokenExpiry = result.ExpiresOn;
        return _cachedToken;
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
            SubscriptionId = SafeGetString(cols, 0) ?? "",
            DisplayName = SafeGetString(cols, 1),
            State = SafeGetString(cols, 2),
            TenantId = SafeGetString(cols, 3)
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
            Name = SafeGetString(cols, 0) ?? "",
            Location = SafeGetString(cols, 1) ?? "",
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
            Name = SafeGetString(cols, 0) ?? "",
            Type = SafeGetString(cols, 1) ?? "",
            Location = SafeGetString(cols, 2),
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
            var body = new Dictionary<string, object>
            {
                ["query"] = kql,
                ["options"] = skipToken != null
                    ? new Dictionary<string, string> { ["resultFormat"] = "table", ["$skipToken"] = skipToken }
                    : new Dictionary<string, string> { ["resultFormat"] = "table" }
            };
            if (subscriptionIds != null)
                body["subscriptions"] = subscriptionIds;

            var json = JsonSerializer.Serialize(body);
            using var request = new HttpRequestMessage(HttpMethod.Post, ArgEndpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();

            // Debug: append ARG query + response to log file
            var debugDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
            Directory.CreateDirectory(debugDir);
            var debugFile = Path.Combine(debugDir, "arg-debug.log");
            var logEntry = $"[{DateTime.Now:HH:mm:ss}] KQL: {kql}\nStatus: {response.StatusCode}\nResponse:\n{responseBody}\n---\n";
            await File.AppendAllTextAsync(debugFile, logEntry);

            using var doc = JsonDocument.Parse(responseBody);

            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("rows", out var rows))
            {
                foreach (var row in rows.EnumerateArray())
                    results.Add(row.EnumerateArray().Select(e => e.Clone()).ToList());
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

    /// <summary>
    /// Safely extract a string from a row column, handling non-string JsonElement types.
    /// </summary>
    private static string? SafeGetString(List<JsonElement> cols, int index)
    {
        if (index >= cols.Count) return null;
        var el = cols[index];
        return el.ValueKind == JsonValueKind.String ? el.GetString()
             : el.ValueKind == JsonValueKind.Null ? null
             : el.ToString();
    }
}
