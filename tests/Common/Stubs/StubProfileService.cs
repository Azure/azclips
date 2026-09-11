 // Copyright (c) Microsoft Corporation.
 // Licensed under the MIT License.

using Core.Abstractions;

namespace Tests.Common.Stubs;

/// <summary>
/// In-memory <see cref="IProfileService"/> stub seeded with a single active "default" profile,
/// preserving the connection state previously provided by the removed AuthContext stub.
/// </summary>
public class StubProfileService : IProfileService
{
    private readonly Dictionary<string, Profile> _profiles;
    private string? _active;
    /// <summary>
    /// Initializes a new instance of the <see cref="StubProfileService"/> class.
    /// </summary>
    /// <param name="seedDefault">Seed a single active "default" profile.</param>
    /// <param name="userPrincipalName">
    /// UPN stamped on the seeded profile. Required by callers that exercise the real
    /// <c>MsalAuthService</c>, because account selection matches the cached account by UPN.
    /// Defaults to null, which preserves the original behaviour for existing callers.
    /// </param>
    public StubProfileService(bool seedDefault = true, string? userPrincipalName = null)
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
                UserPrincipalName = userPrincipalName,
            };
            _profiles[profile.Name] = profile;
            _active = profile.Name;
        }
    }

    // Default profile keys for other services to use; currently SubscriptionId only.
    List<string> IProfileService.GetProfileKeysForDefault() => ["SubscriptionId"];

    string? IProfileService.GetProfileValueByKey(Profile? profile, string key)
    {
        switch (key)
        {
            case "Name":
                return profile?.Name;
            case "SubscriptionId":
                return profile?.SubscriptionId;
            case "TenantId":
                return profile?.TenantId;
            case "ClientId":
                return profile?.ClientId;
            case "CloudName":
                return profile?.CloudName;
            case "AuthMethod":
                return profile?.AuthMethod.ToString();
            case "DefaultLocation":
                return profile?.DefaultLocation;
            case "UserPrincipalName":
                return profile?.UserPrincipalName;
            case "ManagedIdentityId":
                return profile?.ManagedIdentityId;
            default:
                return null;
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
