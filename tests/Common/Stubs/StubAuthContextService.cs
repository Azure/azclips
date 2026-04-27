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
}
