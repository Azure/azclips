namespace Core.Abstractions;

/// <summary>Lists subscriptions for a token so login can pick a default.</summary>
public interface ISubscriptionService
{
    /// <summary>Lists subscriptions visible to the supplied ARM token.</summary>
    Task<IReadOnlyList<SubscriptionInfo>> ListSubscriptionsAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the tenant that owns the specified subscription. When the supplied token belongs to a
    /// different tenant than the subscription, ARM answers with a 401 challenge whose
    /// <c>WWW-Authenticate</c> header advertises the subscription's real tenant via <c>authorization_uri</c>;
    /// otherwise the tenant is read from the subscription payload. Returns <see langword="null"/> when the
    /// owning tenant cannot be determined.
    /// </summary>
    Task<string?> ResolveSubscriptionTenantAsync(string accessToken, string subscriptionId, CancellationToken cancellationToken = default);
}
