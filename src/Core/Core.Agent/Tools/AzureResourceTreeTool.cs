using System.ComponentModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Agent.Tools;

/// <summary>
/// AI Agent tool that fetches the Azure resource hierarchy from ARM REST API
/// and returns a structured tree: Tenant → Subscription → Resource Group → Resource.
/// Uses the shared MSAL cache from Cirrus login.
/// </summary>
public static class AzureResourceTreeTool
{
    private const string ArmBase = "https://management.azure.com";
    // Well-known Azure CLI 1P client ID (public, non-secret). Duplicated from MsalAuthService
    // to avoid coupling Core.Agent → Core.Authentication; must match to share the MSAL token cache.
    private const string ArmScope = "https://management.core.windows.net//.default";
    private const string ClientId = "1950a258-227b-4e31-a9cf-717495945fc2";
    private const string ApiVersion = "2022-12-01";

    private static readonly string CirrusDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");

    /// <summary>
    /// Creates an AIFunction that can be registered with the agent.
    /// </summary>
    public static AIFunction CreateAIFunction()
    {
        return AIFunctionFactory.Create(GetAzureResourceTreeAsync,
            new AIFunctionFactoryOptions
            {
                Name = "get_azure_resource_tree",
                Description = "Fetches the Azure resource hierarchy (tenants, subscriptions, resource groups, and resources) " +
                              "from ARM REST API and returns it as a structured tree. " +
                              "Use this to understand what Azure resources the user has access to. " +
                              "Optionally filter by subscription ID or name, and control tree depth.",
            });
    }

    [Description("Get Azure resource hierarchy as a structured tree. Returns tenants → subscriptions → resource groups → resources.")]
    private static async Task<string> GetAzureResourceTreeAsync(
        [Description("Maximum depth of the tree: 1=tenants, 2=+subscriptions, 3=+resource groups, 4=+resources. Default: 3")]
        int depth = 3,
        [Description("Optional subscription ID or display name to filter by. If omitted, shows all subscriptions.")]
        string? subscriptionFilter = null)
    {
        var token = await AcquireTokenAsync();
        if (token == null)
            return "Error: Not authenticated. Please run 'login' first to authenticate with Azure.";

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var tree = new TreeNode("/", "root");

        // Level 1: Tenants
        var tenants = await GetArmListAsync<TenantInfo>(http, $"{ArmBase}/tenants?api-version={ApiVersion}");
        if (tenants.Count == 0)
            return "No Azure tenants found. Ensure you are logged in with 'login'.";

        foreach (var tenant in tenants)
        {
            var tenantName = tenant.DisplayName ?? tenant.DefaultDomain ?? tenant.TenantId;
            var tenantNode = new TreeNode(tenantName, "tenant", tenant.TenantId);
            tree.Children.Add(tenantNode);

            if (depth < 2) continue;

            // Level 2: Subscriptions
            var subs = await GetArmListAsync<SubscriptionInfo>(http,
                $"{ArmBase}/subscriptions?api-version={ApiVersion}");

            // Filter by tenant
            var tenantSubs = subs.Where(s => s.TenantId == tenant.TenantId).ToList();

            // Apply subscription filter if provided
            if (!string.IsNullOrEmpty(subscriptionFilter))
            {
                tenantSubs = tenantSubs.Where(s =>
                    s.SubscriptionId.Equals(subscriptionFilter, StringComparison.OrdinalIgnoreCase) ||
                    (s.DisplayName?.Contains(subscriptionFilter, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            }

            foreach (var sub in tenantSubs)
            {
                var subName = sub.DisplayName ?? sub.SubscriptionId;
                var subNode = new TreeNode(subName, "subscription", sub.SubscriptionId);
                tenantNode.Children.Add(subNode);

                if (depth < 3) continue;

                // Level 3: Resource Groups
                var rgs = await GetArmListAsync<ResourceGroupInfo>(http,
                    $"{ArmBase}/subscriptions/{sub.SubscriptionId}/resourcegroups?api-version={ApiVersion}");

                foreach (var rg in rgs)
                {
                    var rgNode = new TreeNode(rg.Name, "resourceGroup", rg.Id)
                    {
                        Location = rg.Location
                    };
                    subNode.Children.Add(rgNode);

                    if (depth < 4) continue;

                    // Level 4: Resources
                    var resources = await GetArmListAsync<ResourceInfo>(http,
                        $"{ArmBase}/subscriptions/{sub.SubscriptionId}/resourceGroups/{rg.Name}/resources?api-version={ApiVersion}");

                    foreach (var resource in resources)
                    {
                        rgNode.Children.Add(new TreeNode(resource.Name, "resource", resource.Id)
                        {
                            ResourceType = resource.Type,
                            Location = resource.Location
                        });
                    }
                }
            }
        }

        return JsonSerializer.Serialize(tree, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    // ── Auth ──

    private static async Task<string?> AcquireTokenAsync()
    {
        try
        {
            var app = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority("https://login.microsoftonline.com/common")
                .WithRedirectUri("http://localhost")
                .Build();

            var cacheFile = Path.Combine(CirrusDir, "MSAL.cache.plaintext");
            if (!File.Exists(cacheFile)) return null;

            var storageProps = new StorageCreationPropertiesBuilder("MSAL.cache.plaintext", CirrusDir)
                .WithUnprotectedFile()
                .Build();
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProps);
            cacheHelper.RegisterCache(app.UserTokenCache);

            var accounts = await app.GetAccountsAsync();
            var account = accounts.FirstOrDefault();
            if (account == null) return null;

            var result = await app.AcquireTokenSilent([ArmScope], account)
                .WithForceRefresh(false)
                .ExecuteAsync();

            return result.AccessToken;
        }
        catch
        {
            return null;
        }
    }

    // ── ARM list with pagination ──

    private static async Task<List<T>> GetArmListAsync<T>(HttpClient http, string url)
    {
        var items = new List<T>();
        var nextLink = url;

        while (!string.IsNullOrEmpty(nextLink))
        {
            var response = await http.GetAsync(nextLink);
            if (!response.IsSuccessStatusCode) break;

            var body = await response.Content.ReadFromJsonAsync<ArmListResponse<T>>();
            if (body?.Value != null) items.AddRange(body.Value);
            nextLink = body?.NextLink;
        }

        return items;
    }

    // ── Response models ──

    private class TreeNode
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ResourceType { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Location { get; set; }
        public List<TreeNode> Children { get; set; } = [];

        public TreeNode(string name, string kind, string? id = null)
        {
            Name = name;
            Kind = kind;
            Id = id;
        }
    }

    private class ArmListResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T>? Value { get; set; }
        [JsonPropertyName("nextLink")]
        public string? NextLink { get; set; }
    }

    private class TenantInfo
    {
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = "";
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }
        [JsonPropertyName("defaultDomain")]
        public string? DefaultDomain { get; set; }
    }

    private class SubscriptionInfo
    {
        [JsonPropertyName("subscriptionId")]
        public string SubscriptionId { get; set; } = "";
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }
        [JsonPropertyName("state")]
        public string? State { get; set; }
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; }
    }

    private class ResourceGroupInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("location")]
        public string Location { get; set; } = "";
    }

    private class ResourceInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
        [JsonPropertyName("location")]
        public string? Location { get; set; }
    }
}
