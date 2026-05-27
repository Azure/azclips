using System.Text.Json;
using Azure.Core;
using Azure.ResourceManager;
using Core.Abstractions;

namespace Core.Agent.Tools.Cache;

/// <summary>
/// Live ARM details fetcher used by <c>get_resource_details</c> when the
/// caller (LLM) flips <c>live=true</c>. Uses Azure.ResourceManager so we
/// don't maintain a per-RP api-version mapping. 2-minute process-local
/// cache keeps repeated tool calls in one conversation cheap.
/// </summary>
public sealed class ArmDetailsClient
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    private readonly IAuthService _authService;
    private readonly object _lock = new();
    private ArmClient? _client;
    private readonly Dictionary<string, (DateTime ExpiresAt, JsonElement Body)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ArmDetailsClient(IAuthService authService)
    {
        _authService = authService;
    }

    private ArmClient GetOrCreateClient()
    {
        if (_client is not null) return _client;
        lock (_lock)
        {
            if (_client is not null) return _client;
            // Route ARM SDK auth through the cirrus-internal MSAL pipeline
            // (single ClientId, shared MSAL cache, shared telemetry).
            _client = new ArmClient(new AuthServiceTokenCredential(_authService));
            return _client;
        }
    }

    public async Task<JsonElement?> GetByIdAsync(string armId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(armId)) return null;

        lock (_lock)
        {
            if (_cache.TryGetValue(armId, out var hit) && hit.ExpiresAt > DateTime.UtcNow)
            {
                return hit.Body;
            }
        }

        try
        {
            var client = GetOrCreateClient();
            var resource = client.GetGenericResource(new ResourceIdentifier(armId));
            var response = await resource.GetAsync(ct).ConfigureAwait(false);
            var data = response.Value.Data;

            var json = new
            {
                id = data.Id?.ToString(),
                name = data.Name,
                type = data.ResourceType.ToString(),
                location = data.Location.ToString(),
                kind = data.Kind,
                managedBy = data.ManagedBy,
                sku = data.Sku is null ? null : new
                {
                    name = data.Sku.Name,
                    tier = data.Sku.Tier,
                    size = data.Sku.Size,
                    family = data.Sku.Family,
                    capacity = data.Sku.Capacity,
                },
                tags = data.Tags,
                identity = data.Identity is null ? null : new
                {
                    principalId = data.Identity.PrincipalId,
                    tenantId = data.Identity.TenantId,
                    type = data.Identity.ManagedServiceIdentityType.ToString(),
                },
                plan = data.Plan is null ? null : new
                {
                    name = data.Plan.Name,
                    publisher = data.Plan.Publisher,
                    product = data.Plan.Product,
                },
                provisioningState = data.ProvisioningState,
                properties = data.Properties is null
                    ? (JsonElement?)null
                    : JsonSerializer.Deserialize<JsonElement>(data.Properties.ToString()),
            };
            var body = JsonSerializer.SerializeToElement(json);

            lock (_lock)
            {
                _cache[armId] = (DateTime.UtcNow + CacheTtl, body);
            }
            return body;
        }
        catch (Exception ex)
        {
            return JsonSerializer.SerializeToElement(new
            {
                error = "live_arm_fetch_failed",
                armId,
                exception = ex.GetType().Name,
                message = ex.Message,
            });
        }
    }
}
