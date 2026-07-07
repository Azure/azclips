namespace Core.Abstractions;

/// <summary>Subscription metadata returned by ARM.</summary>
public record SubscriptionInfo(string SubscriptionId, string DisplayName, string State)
{
    /// <summary>True when the subscription state is Enabled.</summary>
    public bool IsEnabled => string.Equals(State, "Enabled", System.StringComparison.OrdinalIgnoreCase);
}
