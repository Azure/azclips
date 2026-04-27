// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Tests.Common.Stubs;

public class StubAuthContextService : IAuthContextService
{
    private AuthContext? _context = new AuthContext(
        TenantId: "00000000-0000-0000-0000-000000000000",
        SubscriptionId: "00000000-0000-0000-0000-000000000000");
    private readonly Dictionary<string, AuthProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeProfileName;

    public Task<AuthContext?> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_context);
    }

    public Task SaveContextAsync(AuthContext authContext, CancellationToken cancellationToken = default)
    {
        _context = authContext;
        return Task.CompletedTask;
    }

    public Task ClearContextAsync(CancellationToken cancellationToken = default)
    {
        _context = null;
        return Task.CompletedTask;
    }

    public Task SaveProfileAsync(
        string profileName,
        AuthContext authContext,
        string? accountId = null,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        _profiles[profileName] = new AuthProfile(
            ProfileName: profileName,
            TenantId: authContext.TenantId,
            SubscriptionId: authContext.SubscriptionId,
            AccountId: accountId ?? "stub-account",
            AccountType: "User",
            Environment: "AzureCloud",
            CreatedAt: now,
            LastUsedAt: now,
            DisplayName: displayName);
        return Task.CompletedTask;
    }

    public Task<AuthProfile?> GetProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        _profiles.TryGetValue(profileName, out var profile);
        return Task.FromResult(profile);
    }

    public Task<IReadOnlyList<AuthProfile>> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AuthProfile>>(_profiles.Values.ToList());
    }

    public Task SetActiveProfileAsync(string profileName, CancellationToken cancellationToken = default)
    {
        if (!_profiles.ContainsKey(profileName))
        {
            throw new InvalidOperationException($"Profile '{profileName}' not found.");
        }

        _activeProfileName = profileName;
        return Task.CompletedTask;
    }

    public Task<AuthProfile?> GetActiveProfileAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_activeProfileName))
        {
            return Task.FromResult<AuthProfile?>(null);
        }

        _profiles.TryGetValue(_activeProfileName, out var active);
        return Task.FromResult(active);
    }

    public Task DeleteProfileAsync(string profileName, bool force = false, CancellationToken cancellationToken = default)
    {
        if (!_profiles.ContainsKey(profileName))
        {
            throw new InvalidOperationException($"Profile '{profileName}' not found.");
        }

        var isActive = string.Equals(_activeProfileName, profileName, StringComparison.OrdinalIgnoreCase);
        if (isActive && !force)
        {
            throw new InvalidOperationException(
                $"Profile '{profileName}' is the active profile. Re-run with --force to delete it.");
        }

        _profiles.Remove(profileName);
        if (isActive)
        {
            _activeProfileName = null;
        }
        return Task.CompletedTask;
    }

    public Task<AuthProfile> RenameProfileAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("New profile name must differ from the existing name.", nameof(newName));
        }
        if (!_profiles.TryGetValue(oldName, out var existing))
        {
            throw new InvalidOperationException($"Profile '{oldName}' not found.");
        }
        if (_profiles.ContainsKey(newName))
        {
            throw new InvalidOperationException($"Profile '{newName}' already exists.");
        }

        var renamed = existing with { ProfileName = newName, LastUsedAt = DateTimeOffset.UtcNow };
        _profiles.Remove(oldName);
        _profiles[newName] = renamed;
        if (string.Equals(_activeProfileName, oldName, StringComparison.OrdinalIgnoreCase))
        {
            _activeProfileName = newName;
        }
        return Task.FromResult(renamed);
    }
}
