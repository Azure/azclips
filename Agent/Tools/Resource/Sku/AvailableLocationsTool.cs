using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Core;
using Core.Abstractions;
using Core.Agent.Tools.Resource.Cache;
using Microsoft.Extensions.AI;

namespace Core.Agent.Tools.Resource.Sku;

/// <summary>
/// Agent tool: given a VM SKU, return which Azure locations currently expose
/// it for the caller's subscription. Backs the SkuNotAvailable auto-recovery
/// flow — when ARM rejects a `vm create` with SkuNotAvailable in location X,
/// the agent calls this tool to pick a working location and retry, without
/// going back to the user.
///
/// Wire-level source of truth: ARM <c>providers/Microsoft.Compute/skus</c>
/// with <c>$filter=location eq 'X'</c>. We hit each candidate region in
/// parallel; restrictions[] tells us whether the SKU is blocked at Location
/// or only at Zone level. Auto-approved by ChatAgent because tool name
/// starts with <c>find_</c> (see <c>IsReadOnlyCacheTool</c>).
/// </summary>
public sealed partial class AvailableLocationsTool
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private const string ArmEndpoint = "https://management.azure.com";
    private const string SkusApiVersion = "2021-07-01";
    private static readonly string[] ArmScope = { "https://management.azure.com/.default" };
    private static readonly Regex LocationName = new(@"^[a-z0-9]+$", RegexOptions.Compiled);

    // Default candidate set covers the regions azclips customers hit 95% of
    // the time. Keeping it small bounds tool latency to ~one ARM round-trip
    // (all calls run in parallel). LLM can override with `candidateLocations`.
    private static readonly string[] DefaultCandidates =
    {
        "eastus", "eastus2", "westus2", "westus3",
        "centralus", "southcentralus",
        "northeurope", "westeurope",
    };

    private readonly IAuthService _authService;
    private readonly IProfileService _profileService;
    private readonly HttpClient _http;
    private readonly Func<string, IDisposable?>? _activityCreator;

    public AvailableLocationsTool(
        IAuthService authService, IProfileService profileService, HttpClient? http = null,
        Func<string, IDisposable?>? activityCreator = null)
    {
        _authService = authService;
        _profileService = profileService;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _activityCreator = activityCreator;

        FindAvailableLocations = AIFunctionFactory.Create(FindAvailableLocationsAsync, new AIFunctionFactoryOptions
        {
            Name = "find_available_locations",
            Description = "Given a VM SKU (e.g. Standard_D2s_v5), return which Azure locations " +
                "currently expose it for the caller's subscription. Use this when ARM returns " +
                "SkuNotAvailable on `vm create` — call this tool, pick a location where the SKU " +
                "is available, then retry the create with --location set to that region. " +
                "Returns one entry per candidate location with `available`, `zones`, and the " +
                "restriction `reason` when blocked. Read-only, auto-approved, ~1 ARM round-trip.",
        });

        All = new[] { FindAvailableLocations };
    }

    public AIFunction FindAvailableLocations { get; }

    public IReadOnlyList<AIFunction> All { get; }

    [Description("Find Azure locations where a VM SKU is available.")]
    private async Task<string> FindAvailableLocationsAsync(
        [Description("VM SKU name to check, e.g. 'Standard_D2s_v5'. Required.")]
        string vmSize,
        [Description("Optional explicit list of locations to probe. Defaults to ~8 common regions when omitted.")]
        string[]? candidateLocations = null,
        [Description("Optional subscription id. Defaults to the active profile's subscription.")]
        string? subscriptionId = null,
        CancellationToken ct = default)
    {
        var probed = (candidateLocations is { Length: > 0 } ? candidateLocations : DefaultCandidates)
            .Select(l => l.Trim().ToLowerInvariant())
            .Where(l => l.Length > 0)
            .Distinct()
            .ToArray();

        using var scope = ToolInvocationLog.Start("find_available_locations",
            $"vmSize={vmSize} locations={probed.Length} sub={subscriptionId ?? "auth-context"}", _activityCreator);
        // SKU names are public Azure catalog values; anything else is not recorded.
        scope.RecordRequest(vmSize is not null && SkuNamePattern().IsMatch(vmSize) ? vmSize : "invalid");

        if (string.IsNullOrWhiteSpace(vmSize))
        {
            var err = JsonSerializer.Serialize(new { error = "missing_argument", argument = "vmSize" }, Json);
            scope.SetResult("error=missing_argument");
            scope.RecordResponse("error");
            return err;
        }

        // Resolve subscription
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            var ctx = await _profileService.GetActiveProfileAsync(ct).ConfigureAwait(false);
            subscriptionId = ctx?.SubscriptionId;
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                var err = JsonSerializer.Serialize(new
                {
                    error = "no_subscription",
                    hint = "Pass subscriptionId or set the active profile first.",
                }, Json);
                scope.SetResult("error=no_subscription");
                scope.RecordResponse("error");
                return err;
            }
        }

        string token;
        try
        {
            var tok = await _authService
                .AcquireTokenSilentlyAsync(ArmScope, tenantId: null, promptOnError: false, ct)
                .ConfigureAwait(false);
            token = tok.AccessToken;
        }
        catch (Exception ex)
        {
            var err = JsonSerializer.Serialize(new
            {
                error = "token_acquisition_failed",
                exception = ex.GetType().Name,
                message = ex.Message,
            }, Json);
            scope.SetResult($"error=token_acquisition_failed {ex.GetType().Name}");
            scope.RecordResponse("error");
            return err;
        }

        // Probe each candidate region in parallel — total latency stays at
        // one ARM round-trip regardless of candidate count.
        var tasks = probed.Select(loc => ProbeLocationAsync(token, subscriptionId!, vmSize, loc, ct)).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var available = results.Where(r => r.Available).ToArray();
        var json = JsonSerializer.Serialize(new
        {
            query = new { vmSize, subscriptionId, candidateLocations = probed },
            availableCount = available.Length,
            locations = results,
        }, Json);

        var summary = available.Length == 0
            ? $"{vmSize} not available in any of {probed.Length} probed regions"
            : $"{vmSize} available in {available.Length}/{probed.Length}: {string.Join(", ", available.Select(r => r.Location))}";
        scope.SetResult(summary);
        scope.RecordResponse(available.Length == 0 ? "not_found" : "found");
        return json;
    }

    private async Task<LocationAvailability> ProbeLocationAsync(
        string token, string subscriptionId, string vmSize, string location, CancellationToken ct)
    {
        // location is interpolated into an OData $filter string literal below,
        // and comes from LLM output. Azure region names are lowercase letters
        // and digits only (verified against `az account list-locations`), so
        // reject anything else up-front to keep the model from injecting quote
        // characters or filter clauses into the ARM query.
        if (!LocationName.IsMatch(location))
            return LocationAvailability.Errored(location, 0, "invalid location format");

        var url = $"{ArmEndpoint}/subscriptions/{subscriptionId}/providers/Microsoft.Compute/skus" +
                  $"?api-version={SkusApiVersion}&$filter=location eq '{location}'";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return LocationAvailability.Errored(location, (int)resp.StatusCode, Truncate(body, 200));
            }

            var payload = await resp.Content
                .ReadFromJsonAsync<SkuListResponse>(Json, ct).ConfigureAwait(false);
            if (payload?.Value is null)
                return LocationAvailability.NotListed(location);

            var match = payload.Value.FirstOrDefault(s =>
                string.Equals(s.ResourceType, "virtualMachines", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.Name, vmSize, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return LocationAvailability.NotListed(location);

            var restrictions = match.Restrictions ?? Array.Empty<SkuRestriction>();
            var locationBlock = restrictions.FirstOrDefault(r =>
                string.Equals(r.Type, "Location", StringComparison.OrdinalIgnoreCase));
            var zoneBlock = restrictions.FirstOrDefault(r =>
                string.Equals(r.Type, "Zone", StringComparison.OrdinalIgnoreCase));

            if (locationBlock is not null)
                return LocationAvailability.Blocked(location, "Location", locationBlock.ReasonCode);

            var zones = match.LocationInfo?.FirstOrDefault()?.Zones ?? Array.Empty<string>();
            var blockedZones = zoneBlock?.RestrictionInfo?.Zones ?? Array.Empty<string>();
            var availableZones = zones.Except(blockedZones, StringComparer.OrdinalIgnoreCase).ToArray();

            // SKU listed, no Location-type block — agent can deploy here.
            return new LocationAvailability(
                Location: location,
                Available: true,
                Zones: availableZones,
                BlockedZones: blockedZones.Length > 0 ? blockedZones : null,
                RestrictionType: null,
                Reason: null,
                Error: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return LocationAvailability.Errored(location, statusCode: null, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";

    // ── result shape (kept terse — the LLM reads it) ──

    internal sealed record LocationAvailability(
        string Location,
        bool Available,
        string[]? Zones,
        string[]? BlockedZones,
        string? RestrictionType,
        string? Reason,
        string? Error)
    {
        public static LocationAvailability Blocked(string location, string type, string? reason) =>
            new(location, false, null, null, type, reason ?? "Restricted", null);

        public static LocationAvailability NotListed(string location) =>
            new(location, false, null, null, null, "SkuNotListedInRegion", null);

        public static LocationAvailability Errored(string location, int? statusCode, string detail) =>
            new(location, false, null, null, null, null, $"http={statusCode?.ToString() ?? "n/a"} {detail}");
    }

    // ── ARM wire shape (only fields we care about) ──

    internal sealed class SkuListResponse
    {
        [JsonPropertyName("value")] public SkuEntry[]? Value { get; set; }
    }

    internal sealed class SkuEntry
    {
        [JsonPropertyName("resourceType")] public string? ResourceType { get; set; }
        [JsonPropertyName("name")]          public string? Name { get; set; }
        [JsonPropertyName("locationInfo")]  public LocationInfo[]? LocationInfo { get; set; }
        [JsonPropertyName("restrictions")]  public SkuRestriction[]? Restrictions { get; set; }
    }

    internal sealed class LocationInfo
    {
        [JsonPropertyName("location")] public string? Location { get; set; }
        [JsonPropertyName("zones")]    public string[]? Zones { get; set; }
    }

    internal sealed class SkuRestriction
    {
        [JsonPropertyName("type")]            public string? Type { get; set; }
        [JsonPropertyName("reasonCode")]      public string? ReasonCode { get; set; }
        [JsonPropertyName("restrictionInfo")] public RestrictionInfo? RestrictionInfo { get; set; }
    }

    internal sealed class RestrictionInfo
    {
        [JsonPropertyName("locations")] public string[]? Locations { get; set; }
        [JsonPropertyName("zones")]     public string[]? Zones { get; set; }
    }

    [GeneratedRegex("^[A-Za-z0-9_]{1,64}$")]
    private static partial Regex SkuNamePattern();
}
