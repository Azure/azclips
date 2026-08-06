 // Copyright (c) Microsoft Corporation.
 // Licensed under the MIT License.

using Core.Abstractions;

namespace Tests.Common.Stubs;

/// <summary>
/// In-memory <see cref="IProfileService"/> stub seeded with a single active "default" profile,
/// preserving the connection state previously provided by the removed AuthContext stub.
/// </summary>
/// <remarks>
/// Used by the unit and scenario test suites, where profile persistence is incidental and
/// file I/O would only add cost and flakiness. Benchmarks deliberately do <b>not</b> use this
/// stub: they register the real file-backed <c>ProfileService</c> against a scoped directory
/// (see <c>BenchmarkProfileStore</c>) so profile persistence is actually measured.
/// </remarks>
public class StubProfileService : IProfileService
{
    private readonly Dictionary<string, Profile> _profiles;
    private string? _active;

    public StubProfileService(bool seedDefault = true)
    {
        _profiles = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
        if (seedDefault)
        {
            var profile = new Profile
            {
                Name = "default",
                TenantId = "00000000-0000-0000-0000-000000000000",
                SubscriptionId = "00000000-0000-0000-0000-000000000000",
                CloudName = "AzureCloud",
                AuthMethod = AuthMethod.Browser,
            };
            _profiles[profile.Name] = profile;
            _active = profile.Name;
        }
    }

    public Task<IReadOnlyCollection<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyCollection<Profile>>(_profiles.Values.ToList());

    public Task<Profile?> GetProfileAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(_profiles.TryGetValue(name, out var profile) ? profile : null);

    public Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        _profiles[profile.Name] = profile;
        return Task.CompletedTask;
    }

    public Task SaveAndActivateProfileAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        _profiles[profile.Name] = profile;
        _active = profile.Name;
        return Task.CompletedTask;
    }

    public Task<bool> RemoveProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        bool removed = _profiles.Remove(name);
        if (removed && string.Equals(_active, name, StringComparison.OrdinalIgnoreCase))
        {
            _active = null;
        }
        return Task.FromResult(removed);
    }

    public Task<string?> GetActiveProfileNameAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_active);

    public Task<Profile?> GetActiveProfileAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(string.IsNullOrEmpty(_active)
            ? null
            : _profiles.TryGetValue(_active, out var profile) ? profile : null);

    public Task SetActiveProfileNameAsync(string? name, CancellationToken cancellationToken = default)
    {
        _active = string.IsNullOrEmpty(name) ? null : name;
        return Task.CompletedTask;
    }
}
