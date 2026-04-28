namespace Core.Abstractions;

public interface IProfileService
{
    Task<IReadOnlyList<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default);
    Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default);
    Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default);
    Task SetActiveProfileAsync(string name, CancellationToken cancellationToken = default);
    Task SaveProfileAsync(Profile profile, bool setActive = true, CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(string name, Action<Profile> updates, CancellationToken cancellationToken = default);
    Task DeleteProfileAsync(string name, CancellationToken cancellationToken = default);
}
