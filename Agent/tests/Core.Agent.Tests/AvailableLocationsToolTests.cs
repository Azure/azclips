using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Tools.Resource.Sku;
using Microsoft.Extensions.AI;
using Xunit;

namespace Core.Agent.Tests;

/// <summary>
/// Contract tests for <see cref="AvailableLocationsTool"/>. The agent recovers
/// from <c>SkuNotAvailable</c> by reading this tool's JSON, so we assert the
/// wire shape, not internal formatting.
/// </summary>
public class AvailableLocationsToolTests
{
    private const string SubId = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public async Task FindAvailableLocations_MapsRestrictions_PerLocation()
    {
        var http = new HttpClient(new FakeHandler(loc => loc switch
        {
            "eastus"  => SkuPayload("Standard_D2s_v5", zones: new[] {"1","2","3"}, locationBlocked: true,  zoneBlocked: null),
            "westus3" => SkuPayload("Standard_D2s_v5", zones: new[] {"1","2","3"}, locationBlocked: false, zoneBlocked: null),
            "westus2" => SkuPayload("Standard_D2s_v5", zones: new[] {"1","2","3"}, locationBlocked: false, zoneBlocked: new[] {"1"}),
            "missingregion" => SkuPayload(null, null, false, null), // SKU not listed in region at all
            _ => SkuPayload("Standard_D2s_v5", zones: new[] {"1","2","3"}, locationBlocked: false, zoneBlocked: null),
        }));

        var tool = new AvailableLocationsTool(
            new FakeAuthService(),
            new FakeProfileService(SubId),
            http);

        var root = await InvokeAsync(tool.FindAvailableLocations, new
        {
            vmSize = "Standard_D2s_v5",
            candidateLocations = new[] { "eastus", "westus3", "westus2", "missingregion" },
        });

        var locs = root.GetProperty("locations").EnumerateArray()
            .ToDictionary(e => e.GetProperty("location").GetString()!);

        Assert.False(locs["eastus"].GetProperty("available").GetBoolean());
        Assert.Equal("Location", locs["eastus"].GetProperty("restrictionType").GetString());

        Assert.True(locs["westus3"].GetProperty("available").GetBoolean());
        Assert.Equal(new[] {"1","2","3"},
            locs["westus3"].GetProperty("zones").EnumerateArray().Select(z => z.GetString()).ToArray());

        // Zone-only block — region is usable but with reduced zones.
        Assert.True(locs["westus2"].GetProperty("available").GetBoolean());
        Assert.Equal(new[] {"2","3"},
            locs["westus2"].GetProperty("zones").EnumerateArray().Select(z => z.GetString()).ToArray());
        Assert.Equal(new[] {"1"},
            locs["westus2"].GetProperty("blockedZones").EnumerateArray().Select(z => z.GetString()).ToArray());

        // SKU not listed in that region at all.
        Assert.False(locs["missingregion"].GetProperty("available").GetBoolean());
        Assert.Equal("SkuNotListedInRegion", locs["missingregion"].GetProperty("reason").GetString());

        Assert.Equal(2, root.GetProperty("availableCount").GetInt32());
    }

    [Fact]
    public async Task FindAvailableLocations_MissingVmSize_ReturnsError()
    {
        var tool = new AvailableLocationsTool(
            new FakeAuthService(),
            new FakeProfileService(SubId),
            new HttpClient(new FakeHandler(_ => "{}")));

        var root = await InvokeAsync(tool.FindAvailableLocations, new { vmSize = "" });

        Assert.Equal("missing_argument", root.GetProperty("error").GetString());
    }

    [Fact]
    public async Task FindAvailableLocations_NoSubscriptionInContext_ReturnsError()
    {
        var tool = new AvailableLocationsTool(
            new FakeAuthService(),
            new FakeProfileService(subscriptionId: null),
            new HttpClient(new FakeHandler(_ => "{}")));

        var root = await InvokeAsync(tool.FindAvailableLocations, new { vmSize = "Standard_D2s_v5" });

        Assert.Equal("no_subscription", root.GetProperty("error").GetString());
    }

    [Fact]
    public async Task FindAvailableLocations_HttpFailure_RecordsErrorWithoutThrowing()
    {
        var http = new HttpClient(new FakeHandler(_ => null, statusCode: HttpStatusCode.Forbidden, body: "denied"));
        var tool = new AvailableLocationsTool(
            new FakeAuthService(),
            new FakeProfileService(SubId),
            http);

        var root = await InvokeAsync(tool.FindAvailableLocations, new
        {
            vmSize = "Standard_D2s_v5",
            candidateLocations = new[] { "eastus" },
        });

        var entry = root.GetProperty("locations")[0];
        Assert.False(entry.GetProperty("available").GetBoolean());
        Assert.Contains("403", entry.GetProperty("error").GetString());
    }

    // ── helpers ──

    private static async Task<JsonElement> InvokeAsync(AIFunction fn, object args)
    {
        var argDict = new AIFunctionArguments(
            JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(args))!);
        var result = await fn.InvokeAsync(argDict);
        var json = result switch
        {
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString()!,
            JsonElement je => je.GetRawText(),
            _ => JsonSerializer.Serialize(result),
        };
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static string SkuPayload(string? skuName, string[]? zones, bool locationBlocked, string[]? zoneBlocked)
    {
        if (skuName is null)
            return """{"value":[{"resourceType":"virtualMachines","name":"Other","locationInfo":[],"restrictions":[]}]}""";

        var restrictions = new List<object>();
        if (locationBlocked)
            restrictions.Add(new { type = "Location", reasonCode = "NotAvailableForSubscription" });
        if (zoneBlocked is { Length: > 0 })
            restrictions.Add(new { type = "Zone", reasonCode = "NotAvailableForSubscription",
                                   restrictionInfo = new { zones = zoneBlocked } });

        var doc = new
        {
            value = new[]
            {
                new
                {
                    resourceType = "virtualMachines",
                    name = skuName,
                    locationInfo = new[] { new { location = "x", zones = zones ?? Array.Empty<string>() } },
                    restrictions = restrictions.ToArray(),
                }
            }
        };
        return JsonSerializer.Serialize(doc);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<string, string?> _bodyFor;
        private readonly HttpStatusCode _status;
        private readonly string _failBody;

        public FakeHandler(Func<string, string?> bodyFor, HttpStatusCode statusCode = HttpStatusCode.OK, string body = "")
        {
            _bodyFor = bodyFor;
            _status = statusCode;
            _failBody = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Extract location from $filter=location eq 'X' — query is percent-encoded.
            var q = Uri.UnescapeDataString(request.RequestUri!.Query);
            var idx = q.IndexOf("location eq '", StringComparison.Ordinal);
            var loc = idx < 0 ? "" : q[(idx + "location eq '".Length)..].Split('\'', 2)[0];

            if (_status != HttpStatusCode.OK)
                return Task.FromResult(new HttpResponseMessage(_status)
                    { Content = new StringContent(_failBody, Encoding.UTF8, "text/plain") });

            var body = _bodyFor(loc) ?? "{\"value\":[]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FakeAuthService : IAuthService
    {
        public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task LogoutAsync(string? userPrincipalName, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default, Profile? profile = null) =>
            Task.FromResult(new AuthResult
            {
                AccessToken = "fake-token",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = scopes ?? Array.Empty<string>(),
                TenantId = "fake-tenant",
                CorrelationId = Guid.NewGuid(),
            });
        public Task<bool> CanAcquireTokenSilentlyAsync(string? tenantId, Profile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeProfileService : IProfileService
    {
        private readonly Profile? _profile;
        public FakeProfileService(string? subscriptionId) =>
            _profile = subscriptionId is null
                ? null
                : new Profile { Name = "default", TenantId = "fake-tenant", SubscriptionId = subscriptionId, CloudName = "AzureCloud" };

        public Task<IReadOnlyCollection<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Profile>>(_profile is null ? Array.Empty<Profile>() : new[] { _profile });
        public Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profile is not null && _profile.Name == name ? _profile : null);
        public Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAndActivateProfileAsync(Profile profile, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> RemoveProfileAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<string?> GetActiveProfileNameAsync(CancellationToken cancellationToken = default) => Task.FromResult(_profile?.Name);
        public Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) => Task.FromResult(_profile);
        public Task SetActiveProfileNameAsync(string? name, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public List<string> GetProfileKeysForDefault() => throw new NotImplementedException();
        public string? GetProfileValueByKey(Profile? profile, string key) => throw new NotImplementedException();
    }
}
