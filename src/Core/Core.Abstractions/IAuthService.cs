// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Service to handle user authentication.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Logs in a user with the specified login request.
    /// </summary>
    /// <param name="request">The login request containing login details.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The result of the authentication attempt.</returns>
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs out a single profile, evicting whatever credentials its authentication method cached.
    /// </summary>
    /// <param name="profileName">
    /// The name of the profile being signed out. The implementation resolves the record itself, so a
    /// caller holding only the active-profile pointer need not load it first.
    /// </param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous logout operation.</returns>
    Task LogoutAsync(string profileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a token silently for the specified scopes and tenant.
    /// </summary>
    /// <param name="scopes">The scopes for which the token is requested.</param>
    /// <param name="tenantId">The tenant ID for which the token is requested.</param>
    /// <param name="promptOnError">Whether to prompt the user on error.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="profile">
    /// The explicit identity for this token request. The engine supplies the invocation profile;
    /// reusable credentials supply their bound profile. Null retains the fallback for tooling.
    /// </param>
    /// <returns>The result of the token acquisition attempt.</returns>
    Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default, Profile? profile = null);

    /// <summary>
    /// Whether a token can be acquired for <paramref name="tenantId"/> without prompting. False when
    /// interaction is required; unrelated failures (network, and so on) are propagated.
    /// </summary>
    /// <param name="tenantId">The tenant to acquire the token for.</param>
    /// <param name="profile">The profile whose authentication method drives token acquisition.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    Task<bool> CanAcquireTokenSilentlyAsync(string? tenantId, Profile profile, CancellationToken cancellationToken = default);
}
