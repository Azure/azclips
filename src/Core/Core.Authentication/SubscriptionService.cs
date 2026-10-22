using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Core.Abstractions;

namespace Core.Authentication;

/// <summary>Lists subscriptions with the supplied ARM token, outside the normal auth pipeline.</summary>
public class SubscriptionService(HttpClient httpClient, ILoggingService logger) : ISubscriptionService
{
    private const string ArmBaseUrl = "https://management.azure.com";
    private const string ApiVersion = "2022-12-01";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<SubscriptionInfo>> ListSubscriptionsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var results = new List<SubscriptionInfo>();
        var seenLinks = new HashSet<string>(StringComparer.Ordinal);
        string? uri = $"{ArmBaseUrl}/subscriptions?api-version={ApiVersion}";

        while (!string.IsNullOrEmpty(uri))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            SubscriptionListResult? page = JsonSerializer.Deserialize<SubscriptionListResult>(body, s_jsonOptions);

            if (page?.Value != null)
            {
                foreach (var s in page.Value)
                {
                    if (!string.IsNullOrEmpty(s.SubscriptionId))
                    {
                        results.Add(new SubscriptionInfo(s.SubscriptionId, s.DisplayName ?? string.Empty, s.State ?? string.Empty));
                    }
                }
            }

            uri = page?.NextLink;
            // stop if nextLink loops
            if (!string.IsNullOrEmpty(uri) && !seenLinks.Add(uri))
            {
                logger.Debug($"[{nameof(SubscriptionService)}] Cyclic nextLink detected; stopping pagination.");
                break;
            }
        }

        return results;
    }

    public async Task<string?> ResolveSubscriptionTenantAsync(string accessToken, string? subscriptionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(subscriptionId))
        {
            return null;
        }

        var uri = $"{ArmBaseUrl}/subscriptions/{subscriptionId}?api-version={ApiVersion}";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        // A subscription homed in a different tenant than the token yields a 401/403 challenge whose
        // WWW-Authenticate header advertises the subscription's real tenant via authorization_uri.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            var tenantFromChallenge = ExtractTenantFromChallenge(response);
            if (!string.IsNullOrEmpty(tenantFromChallenge))
            {
                logger.Debug($"[{nameof(SubscriptionService)}] Resolved tenant '{tenantFromChallenge}' for subscription '{subscriptionId}' from the authentication challenge.");
                return tenantFromChallenge;
            }

            logger.Debug($"[{nameof(SubscriptionService)}] Subscription '{subscriptionId}' returned {(int)response.StatusCode} without a usable tenant challenge.");
        }

        // Any non-success status that wasn't a usable cross-tenant challenge is a genuine lookup
        // failure. Throw (rather than returning null) so the caller can distinguish "could not
        // determine the owning tenant" from a definitive "the signed-in tenant owns it" and warn
        // accordingly instead of silently persisting an unverified tenant.
        response.EnsureSuccessStatusCode();

        // The token already has access, so the subscription payload carries the owning tenant.
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var subscription = JsonSerializer.Deserialize<SubscriptionEntry>(body, s_jsonOptions);
        return string.IsNullOrEmpty(subscription?.TenantId) ? null : subscription.TenantId;
    }

    /// <summary>Extracts the tenant id from an ARM authentication challenge's authorization_uri, if present.</summary>
    private static string? ExtractTenantFromChallenge(HttpResponseMessage response)
    {
        foreach (var header in response.Headers.WwwAuthenticate)
        {
            var parameter = header.Parameter;
            if (string.IsNullOrEmpty(parameter))
            {
                continue;
            }

            // e.g. Bearer authorization_uri="https://login.microsoftonline.com/<tenant>", error="invalid_token"
            var match = Regex.Match(parameter, "authorization_uri=\"?([^\",]+)\"?", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            if (Uri.TryCreate(match.Groups[1].Value.Trim(), UriKind.Absolute, out var authUri))
            {
                var tenant = authUri.Segments.LastOrDefault()?.Trim('/');
                if (Guid.TryParse(tenant, out _))
                {
                    return tenant;
                }
            }
        }

        return null;
    }

    private sealed class SubscriptionListResult
    {
        [JsonPropertyName("value")]
        public SubscriptionEntry[]? Value { get; set; }

        [JsonPropertyName("nextLink")]
        public string? NextLink { get; set; }
    }

    private sealed class SubscriptionEntry
    {
        [JsonPropertyName("subscriptionId")]
        public string? SubscriptionId { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; }
    }
}
