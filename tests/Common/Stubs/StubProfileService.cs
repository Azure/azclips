using Core.Abstractions;

namespace Tests.Common.Stubs;

public class StubProfileService : IProfileService
{
    private readonly Dictionary<string, Profile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeProfile;

    public Task<IReadOnlyList<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Profile>>(_profiles.Values.ToList().AsReadOnly());
    }

    public Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        _profiles.TryGetValue(name, out var profile);
        return Task.FromResult(profile);
    }

    public Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        if (_activeProfile is not null && _profiles.TryGetValue(_activeProfile, out var profile))
            return Task.FromResult<Profile?>(profile);
        return Task.FromResult<Profile?>(null);
    }

    public Task SetActiveProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!_profiles.ContainsKey(name))
        {
            var available = string.Join(", ", _profiles.Keys);
            throw new InvalidOperationException(
                $"Profile '{name}' not found. Available profiles: {(string.IsNullOrEmpty(available) ? "(none)" : available)}");
        }
        _activeProfile = name;
        return Task.CompletedTask;
    }

    public Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        Profile.ValidateName(profile.Name);
        _profiles[profile.Name] = profile;
        return Task.CompletedTask;
    }

    public Task<bool> RemoveProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!_profiles.Remove(name))
            return Task.FromResult(false);
        if (string.Equals(_activeProfile, name, StringComparison.OrdinalIgnoreCase))
            _activeProfile = null;
        return Task.FromResult(true);
    }
}
