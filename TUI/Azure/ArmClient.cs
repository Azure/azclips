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
    /// Fetch the entire resource tree (subscriptions, resource groups, resources) in a single ARG query.
    /// </summary>
    public async Task<AzureResourceTree> FetchResourceTreeAsync()
    {
        const string kql = """
            ResourceContainers
            | where type == 'microsoft.resources/subscriptions'
            | project subscriptionId, name, type = type, location = '', resourceGroup = '', properties_state = tostring(properties.state), tenantId
            | union (
                ResourceContainers
                | where type == 'microsoft.resources/subscriptions/resourcegroups'
                | project subscriptionId, name, type = type, location, resourceGroup = '', properties_state = tostring(properties.provisioningState), tenantId
            )
            | union (
                Resources
                | project subscriptionId, name, type, location, resourceGroup, properties_state = '', tenantId
            )
            """;

        var rows = await QueryArgAsync(kql, subscriptionIds: null);
        var tree = new AzureResourceTree();

        // Temporary lookups to group while iterating
        var rgsBySubId = new Dictionary<string, List<AzureResourceGroup>>(StringComparer.OrdinalIgnoreCase);
        var resByKey = new Dictionary<string, List<AzureResource>>(StringComparer.OrdinalIgnoreCase);
        var seenSubs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cols in rows)
        {
            var subId = SafeGetString(cols, 0) ?? "";
            var name = SafeGetString(cols, 1) ?? "";
            var type = SafeGetString(cols, 2) ?? "";
            var location = SafeGetString(cols, 3) ?? "";
            var resourceGroup = SafeGetString(cols, 4) ?? "";
            var state = SafeGetString(cols, 5);
            var tenantId = SafeGetString(cols, 6);

            if (type.Equals("microsoft.resources/subscriptions", StringComparison.OrdinalIgnoreCase))
            {
                if (seenSubs.Add(subId))
                {
                    tree.Subscriptions.Add(new AzureSubscription
                    {
                        SubscriptionId = subId,
                        DisplayName = name,
                        State = state,
                        TenantId = tenantId,
                    });
                }
            }
            else if (type.Equals("microsoft.resources/subscriptions/resourcegroups", StringComparison.OrdinalIgnoreCase))
            {
                if (!rgsBySubId.TryGetValue(subId, out var rgList))
                {
                    rgList = [];
                    rgsBySubId[subId] = rgList;
                }
                rgList.Add(new AzureResourceGroup { Name = name, Location = location });
            }
            else
            {
                var key = $"{subId}/{resourceGroup}";
                if (!resByKey.TryGetValue(key, out var resList))
                {
                    resList = [];
                    resByKey[key] = resList;
                }
                resList.Add(new AzureResource { Name = name, Type = type, Location = location });
            }
        }

        foreach (var (k, v) in rgsBySubId) tree.ResourceGroups[k] = v;
        foreach (var (k, v) in resByKey) tree.Resources[k] = v;

        return tree;
    }

    /// <summary>
    /// Resolve the display name for a tenant via ARM /tenants API.
    /// Returns null if the tenant is not found or the call fails.
    /// </summary>
    public async Task<string?> GetTenantDisplayNameAsync(string tenantId)
    {
        try
        {
            var token = await AcquireTokenAsync(tenantId);
            if (token == null) return null;

            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://management.azure.com/tenants?api-version=2022-12-01");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("value", out var tenants))
            {
                foreach (var tenant in tenants.EnumerateArray())
                {
                    if (tenant.TryGetProperty("tenantId", out var tid) &&
                        tid.GetString()?.Equals(tenantId, StringComparison.OrdinalIgnoreCase) == true &&
                        tenant.TryGetProperty("displayName", out var dn))
                    {
                        return dn.GetString();
                    }
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Fetch only the subscription display name for a given subscription ID via a lightweight ARG query.
    /// </summary>
    public async Task<string?> FetchSubscriptionDisplayNameAsync(string subscriptionId)
    {
        const string kql = """
            ResourceContainers
            | where type == 'microsoft.resources/subscriptions'
            | project subscriptionId, name
            """;

        var rows = await QueryArgAsync(kql, [subscriptionId]);
        foreach (var cols in rows)
        {
            var subId = SafeGetString(cols, 0) ?? "";
            if (subId.Equals(subscriptionId, StringComparison.OrdinalIgnoreCase))
                return SafeGetString(cols, 1);
        }
        return null;
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
