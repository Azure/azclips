namespace Core.Abstractions;

public class AuthenticationInfo
{
    public required string SubscriptionId { get; init; }
    public required string TenantId { get; init; }
    public bool IsAuthenticated { get; init; }
}
