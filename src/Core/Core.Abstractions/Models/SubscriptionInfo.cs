namespace Core.Abstractions;

/// <summary>
/// Minimal subscription metadata returned by the ARM "Subscriptions - List" API
/// (<c>GET /subscriptions</c>). Mirrors the fields Azure CLI / Azure PowerShell use
/// when enumerating subscriptions after login.
/// </summary>
public record SubscriptionInfo(string SubscriptionId, string DisplayName, string State)
{
    /// <summary>
    /// Whether the subscription is in the "Enabled" state. Azure CLI prefers the first
    /// Enabled subscription when choosing a default.
    /// </summary>
    public bool IsEnabled => string.Equals(State, "Enabled", System.StringComparison.OrdinalIgnoreCase);
}
