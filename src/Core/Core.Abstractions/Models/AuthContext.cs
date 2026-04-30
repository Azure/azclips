namespace Core.Abstractions;

/// <summary>
/// Contextual information about the authentication state.
/// </summary>
public record AuthContext(string TenantId, string SubscriptionId)
{
    public override string ToString() =>
        $"Subscription: {SubscriptionId}{System.Environment.NewLine}Tenant: {TenantId}";
}