namespace Core.Abstractions;

/// <summary>
/// Service for managing named connection profiles and the active-profile pointer.
/// Profiles encompass configuration beyond authentication and are the single source
/// of truth for connection state.
/// </summary>
public interface IProfileService
{
    /// <summary>Gets all saved profiles.</summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>All saved profiles; empty when none exist.</returns>
    Task<IReadOnlyCollection<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the profile keys for other services to use as default.</summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>List of profile keys for other services to use as default.</returns>
    List<string> GetProfileKeysForDefault();

    /// <summary>Gets the value of a profile key from the active profile, or null if it does not exist.</summary>
    /// <param name="key">The profile key.</param>
    /// <returns>The value of the profile key, or null if it does not exist.</returns>
    string? GetProfileValueByKey(Profile? profile,string key);

    /// <summary>Gets a single profile by name.</summary>
    /// <param name="name">The profile name.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The matching profile, or null if it does not exist.</returns>
    Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Creates or overwrites a profile. The write is atomic.</summary>
    /// <param name="profile">The profile to persist.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or overwrites a profile and sets it as the active profile in a single operation.
    /// Implementations SHOULD perform both writes under one lock to minimize file I/O. Both writes are atomic.
    /// </summary>
    /// <param name="profile">The profile to persist and activate.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    Task SaveAndActivateProfileAsync(Profile profile, CancellationToken cancellationToken = default);

    /// <summary>Removes a profile by name. If it is the active profile, the active pointer is cleared.</summary>
    /// <param name="name">The profile name.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>True if a profile was removed; false if it did not exist.</returns>
    Task<bool> RemoveProfileAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Gets the active profile name, or null if none is set.</summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    Task<string?> GetActiveProfileNameAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the active profile, or null when no active profile is set (or it no longer exists).
    /// Implementations SHOULD resolve the active pointer and load the profile under a single lock to
    /// minimize file I/O.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets (or clears, when null) the active profile name. The write is atomic.
    /// </summary>
    /// <param name="name">The profile name to activate, or null to clear the active pointer.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    Task SetActiveProfileNameAsync(string? name, CancellationToken cancellationToken = default);
}
