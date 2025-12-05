namespace Core.Abstractions;

/// <summary>
/// Provides access to Azure authentication credentials
/// </summary>
public interface IAuthenticationContext
{
    /// <summary>
    /// Gets the current Azure credential
    /// </summary>
    object GetCredential();

    /// <summary>
    /// Gets the current subscription ID if set
    /// </summary>
    string? SubscriptionId { get; }

    /// <summary>
    /// Gets the current tenant ID if set
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Indicates whether the user is authenticated
    /// </summary>
    bool IsAuthenticated { get; }
}
