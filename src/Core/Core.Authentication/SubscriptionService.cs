using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;

namespace Core.Authentication;

/// <summary>
/// Lists Azure subscriptions via the ARM "Subscriptions - List" REST API
/// (<c>GET https://management.azure.com/subscriptions</c>).
/// <para>
/// This is a self-contained call: it attaches the supplied access token directly rather
/// than going through the runtime <c>HttpPipeline</c>, because during login the pipeline's
/// auth handler is still bound to the previous (or empty) auth context and would overwrite
/// the Authorization header with the wrong token.
/// </para>
/// Mirrors Azure CLI's <c>SubscriptionFinder.find_using_specific_tenant</c>.
/// </summary>
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
            // Guard against a service returning a cyclic nextLink.
            if (!string.IsNullOrEmpty(uri) && !seenLinks.Add(uri))
            {
                logger.Debug($"[{nameof(SubscriptionService)}] Cyclic nextLink detected; stopping pagination.");
                break;
            }
        }

        return results;
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
    }
}
