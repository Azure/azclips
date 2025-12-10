namespace Core.Abstractions;

public class LoginRequest
{
    public string? TenantId { get; init; }
    public string? SubscriptionId { get; init; }
}
