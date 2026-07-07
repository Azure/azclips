namespace Core.Abstractions;

/// <summary>Lists subscriptions for a token so login can pick a default.</summary>
public interface ISubscriptionService
{
    /// <summary>Lists subscriptions visible to the supplied ARM token.</summary>
    Task<IReadOnlyList<SubscriptionInfo>> ListSubscriptionsAsync(string accessToken, CancellationToken cancellationToken = default);
}
