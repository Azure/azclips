using Core.Abstractions;

namespace Cli.Tests.Common;

public class StubProfileService : IProfileService
{
    private readonly Dictionary<string, Profile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeProfile;

    public Task<IReadOnlyList<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Profile>>(_profiles.Values.ToList().AsReadOnly());

    public Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(_profiles.TryGetValue(name, out var profile) ? profile : null);

    public Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        if (_activeProfile is not null && _profiles.TryGetValue(_activeProfile, out var profile))
            return Task.FromResult<Profile?>(profile);
        return Task.FromResult<Profile?>(null);
    }

    public Task SetActiveProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!_profiles.ContainsKey(name))
            throw new InvalidOperationException($"Profile '{name}' not found.");
        _activeProfile = name;
        return Task.CompletedTask;
    }

    public Task SaveProfileAsync(Profile profile, bool setActive = true, CancellationToken cancellationToken = default)
    {
        _profiles[profile.Name] = profile;
        if (setActive)
            _activeProfile = profile.Name;
        return Task.CompletedTask;
    }

    public Task UpdateProfileAsync(string name, Action<Profile> updates, CancellationToken cancellationToken = default)
    {
        if (!_profiles.TryGetValue(name, out var profile))
            throw new InvalidOperationException($"Profile '{name}' not found.");
        updates(profile);
        return Task.CompletedTask;
    }

    public Task DeleteProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!_profiles.Remove(name))
            throw new InvalidOperationException($"Profile '{name}' not found.");
        if (string.Equals(_activeProfile, name, StringComparison.OrdinalIgnoreCase))
            _activeProfile = null;
        return Task.CompletedTask;
    }
}
