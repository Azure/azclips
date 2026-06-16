namespace Core.Abstractions;

/// <summary>
/// Enumerates the Azure subscriptions accessible to a given access token.
/// Used after login to discover and persist a default subscription, mirroring the
/// behavior of Azure CLI (<c>az login</c>) and Azure PowerShell (<c>Connect-AzAccount</c>),
/// which list subscriptions and select a default rather than requiring one up front.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Lists subscriptions visible to the supplied ARM access token via the
    /// "Subscriptions - List" REST API (<c>GET /subscriptions</c>), following pagination.
    /// </summary>
    /// <param name="accessToken">An ARM-scoped bearer access token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All accessible subscriptions; empty when the identity has tenant-level access only.</returns>
    Task<IReadOnlyList<SubscriptionInfo>> ListSubscriptionsAsync(string accessToken, CancellationToken cancellationToken = default);
}
