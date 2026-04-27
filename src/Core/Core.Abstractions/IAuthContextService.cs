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

    /// <summary>
    /// Saves profile metadata for a named profile.
    /// </summary>
    /// <param name="profileName">Profile name.</param>
    /// <param name="authContext">Authentication context defaults.</param>
    /// <param name="accountId">Optional account identifier.</param>
    /// <param name="displayName">Optional display name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SaveProfileAsync(
        string profileName,
        AuthContext authContext,
        string? accountId = null,
        string? displayName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a named profile.
    /// </summary>
    /// <param name="profileName">Profile name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The profile, or null if not found.</returns>
    Task<AuthProfile?> GetProfileAsync(string profileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all valid profiles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All valid profiles.</returns>
    Task<IReadOnlyList<AuthProfile>> ListProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active profile.
    /// </summary>
    /// <param name="profileName">Profile name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SetActiveProfileAsync(string profileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active profile.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active profile, or null if no valid active profile is set.</returns>
    Task<AuthProfile?> GetActiveProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a named profile. If the profile is currently active and <paramref name="force"/> is false, the call fails.
    /// When the active profile is deleted with <paramref name="force"/> = true, the active-profile pointer is cleared.
    /// </summary>
    /// <param name="profileName">Profile name.</param>
    /// <param name="force">Allow deleting the active profile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeleteProfileAsync(string profileName, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a named profile, preserving its metadata. If the source profile is active, the active-profile
    /// pointer is updated to the new name.
    /// </summary>
    /// <param name="oldName">Existing profile name.</param>
    /// <param name="newName">New profile name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The renamed profile.</returns>
    Task<AuthProfile> RenameProfileAsync(string oldName, string newName, CancellationToken cancellationToken = default);
}
