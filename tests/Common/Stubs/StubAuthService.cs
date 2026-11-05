// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;
using System.Collections.Concurrent;

namespace Tests.Common.Stubs;

public class StubAuthService : IAuthService
{
    private static AuthResult CreateFakeResult() => new()
    {
        AccessToken = "stub-access-token-for-benchmark",
        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
        Scopes = Array.Empty<string>(),
        TenantId = "00000000-0000-0000-0000-000000000000",
        EntraObjectId = "11111111-2222-3333-4444-555555555555",
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

    private int _acquireCount;
    private readonly ConcurrentQueue<(string[]? Scopes, Profile? Profile)> _tokenAcquisitions = new();

    public IReadOnlyList<(string[]? Scopes, Profile? Profile)> TokenAcquisitions => _tokenAcquisitions.ToArray();

    /// <summary>The number of <see cref="AcquireTokenSilentlyAsync"/> calls.</summary>
    public int AcquireCount => Volatile.Read(ref _acquireCount);

    /// <summary>The scopes passed to the most recent <see cref="AcquireTokenSilentlyAsync"/> call.</summary>
    public string[]? LastScopes { get; private set; }

    /// <summary>The <c>promptOnError</c> value passed to the most recent <see cref="AcquireTokenSilentlyAsync"/> call.</summary>
    public bool? LastPromptOnError { get; private set; }

    /// <summary>The cancellation token passed to the most recent <see cref="AcquireTokenSilentlyAsync"/> call.</summary>
    public CancellationToken LastCancellationToken { get; private set; }

    public Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default, Profile? profile = null)
    {
        Interlocked.Increment(ref _acquireCount);
        _tokenAcquisitions.Enqueue((scopes is null ? null : [.. scopes], profile));
        LastScopes = scopes;
        LastPromptOnError = promptOnError;
        LastCancellationToken = cancellationToken;
        return AcquireTokenAsync(scopes, tenantId, cancellationToken);
    }

    /// <summary>Produces the result of <see cref="AcquireTokenSilentlyAsync"/> after the call is recorded.</summary>
    protected virtual Task<AuthResult> AcquireTokenAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken)
    {
        return Task.FromResult(CreateFakeResult());
    }

    public virtual Task<bool> CanAcquireTokenSilentlyAsync(string? tenantId, Profile profile, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
