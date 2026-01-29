namespace Core.Abstractions;

/// <summary>
/// Contextual information about the authentication state.
/// </summary>
public class AuthContext
{
    /// <summary>
    /// The tenant ID associated with the authentication context.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// The subscription ID associated with the authentication context.
    /// </summary>
    public required string SubscriptionId { get; init; }
}