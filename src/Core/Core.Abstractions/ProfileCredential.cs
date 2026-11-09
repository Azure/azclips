// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;

namespace Core.Abstractions;

/// <summary>Binds a stable identity to an existing token acquisition delegate without owning a token cache.</summary>
public sealed class ProfileCredential : IProfileCredential
{
    private readonly AcquireTokenAsyncDelegate acquireToken;

    public ProfileCredential(Profile profile, AcquireTokenAsyncDelegate acquireToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(acquireToken);
        Profile = profile;
        this.acquireToken = acquireToken;
    }

    public Profile Profile { get; }

    public Task<AuthResult> GetTokenAsync(string[]? scopes = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return acquireToken(scopes is null ? null : [.. scopes], Profile.TenantId, false, cancellationToken, Profile);
    }
}
