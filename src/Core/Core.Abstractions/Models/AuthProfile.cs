namespace Core.Abstractions;

/// <summary>
/// Persisted profile metadata used to resolve authentication context defaults.
/// </summary>
public sealed record AuthProfile(
    string ProfileName,
    string TenantId,
    string SubscriptionId,
    string AccountId,
    string AccountType,
    string Environment,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt,
    string? DisplayName = null,
    int Version = 1)
{
    public AuthContext ToAuthContext() => new(TenantId, SubscriptionId);
}