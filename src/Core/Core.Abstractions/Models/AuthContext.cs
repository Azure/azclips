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

    /// <summary>
    /// Returns a string representation of the AuthContext.
    /// This can be useful for logging or debugging purposes.
    /// </summary>
    /// <returns>A string that represents the current AuthContext.</returns>
    public override string ToString()
    {
        return $"Subscription: {this.SubscriptionId}{System.Environment.NewLine}Environment: {this.TenantId}";
    }
}