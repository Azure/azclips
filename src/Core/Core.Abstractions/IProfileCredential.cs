// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>A reusable identity backed by the authentication service and its existing token cache.</summary>
public interface IProfileCredential
{
    /// <summary>The identity selected for this credential; it must remain stable across requests.</summary>
    Profile Profile { get; }

    Task<AuthResult> GetTokenAsync(string[] scopes, CancellationToken cancellationToken = default);
}
