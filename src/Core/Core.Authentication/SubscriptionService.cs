using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
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
