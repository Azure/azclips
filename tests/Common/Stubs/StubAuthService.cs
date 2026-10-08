// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;

namespace Tests.Common.Stubs;

public class StubAuthService : IAuthService
{
    private static AuthResult CreateFakeResult() => new()
    {
        AccessToken = "stub-access-token-for-benchmark",
        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
        Scopes = Array.Empty<string>(),
        TenantId = "00000000-0000-0000-0000-000000000000",
        CorrelationId = Guid.Empty
    };

    public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateFakeResult());
    }

    /// <summary>True when <see cref="LogoutAsync"/> has been invoked at least once.</summary>
    public bool LogoutCalled { get; private set; }

    /// <summary>The profile name passed to the most recent <see cref="LogoutAsync"/> call.</summary>
    public string? LastLoggedOutProfileName { get; private set; }

    public Task LogoutAsync(string profileName, CancellationToken cancellationToken = default)
    {
        LogoutCalled = true;
        LastLoggedOutProfileName = profileName;
        return Task.CompletedTask;
    }

    public Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default, Profile? profile = null)
    {
        return Task.FromResult(CreateFakeResult());
    }

    public Task<bool> CanAcquireTokenSilentlyAsync(string? tenantId, Profile profile, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
