namespace Core.Abstractions;

/// <summary>
/// Service to manage named connection profiles. Profiles persist identity context
/// (tenant, subscription, cloud, auth method) and extensible defaults.
/// Tokens/credentials are NOT stored in profiles — MSAL manages those separately.
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// Returns all defined profiles.
    /// </summary>
    Task<IReadOnlyList<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a profile by name (case-insensitive), or null if not found.
    /// </summary>
    Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the currently active profile, or null if none is set.
    /// </summary>
    Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active profile pointer. Throws if the profile does not exist.
    /// </summary>
    Task SetActiveProfileAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a profile (upsert by name).
    /// </summary>
    Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a profile by name. Returns false if not found.
    /// If the removed profile was active, the active pointer is cleared.
    /// </summary>
    Task<bool> RemoveProfileAsync(string name, CancellationToken cancellationToken = default);
}
