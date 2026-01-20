namespace Core.Abstractions;

/// <summary>
/// Service to manage authentication context (e.g., current user, tenant and subscription).
/// </summary>
public interface IAuthContextService
{
    /// <summary>
    /// Gets the current authentication context.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The current authentication context, or null if none is set.</returns>
    Task<AuthContext?> GetCurrentContextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the given authentication context.
    /// </summary>
    /// <param name="authContext">The authentication context to save.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SaveContextAsync(AuthContext authContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the current authentication context. The persisted context file will be removed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous clear operation.</returns>
    Task ClearContextAsync(CancellationToken cancellationToken = default);
}
