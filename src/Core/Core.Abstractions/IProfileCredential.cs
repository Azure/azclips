// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>A reusable identity backed by the authentication service and its existing token cache.</summary>
public interface IProfileCredential
{
    /// <summary>The identity selected for this credential; it must remain stable across requests.</summary>
    Profile Profile { get; }

    /// <summary>Acquires a token, using the authentication service's default audience when scopes are null.</summary>
    Task<AuthResult> GetTokenAsync(string[]? scopes = null, CancellationToken cancellationToken = default);
}
