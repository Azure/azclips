namespace Core.Abstractions;

/// <summary>Lists subscriptions for a token so login can pick a default.</summary>
public interface ISubscriptionService
{
    /// <summary>Lists subscriptions visible to the supplied ARM token.</summary>
    Task<IReadOnlyList<SubscriptionInfo>> ListSubscriptionsAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a subscription by ID.
    /// </summary>
    Task<SubscriptionInfo> GetSubscriptionByIdAsync(string accessToken, string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the tenant that owns the specified subscription. When the supplied token belongs to a
    /// different tenant than the subscription, ARM answers with a 401/403 challenge whose
    /// <c>WWW-Authenticate</c> header advertises the subscription's real tenant via <c>authorization_uri</c>;
    /// otherwise the tenant is read from the subscription payload.
    /// </summary>
    /// <returns>
    /// The owning tenant id; or <see langword="null"/> when <paramref name="subscriptionId"/> is empty or the
    /// subscription payload carries no tenant id.
    /// </returns>
    /// <exception cref="System.Net.Http.HttpRequestException">
    /// Thrown when the request fails with a non-success status that is not a usable cross-tenant challenge
    /// (i.e. the owning tenant genuinely could not be determined). Callers should treat this as a hard failure
    /// rather than "the signed-in tenant owns it", so an unverified tenant is never persisted.
    /// </exception>
    Task<string?> ResolveSubscriptionTenantAsync(string accessToken, string? subscriptionId, CancellationToken cancellationToken = default);
}
