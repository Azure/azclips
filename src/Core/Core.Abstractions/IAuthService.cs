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
    /// Signs out a single account by removing it from the MSAL token cache. The account is identified
    /// by its user principal name (MSAL <c>IAccount.Username</c>). When <paramref name="userPrincipalName"/>
    /// is null or empty, no account is removed.
    /// </summary>
    /// <param name="userPrincipalName">The user principal name of the account to remove from the token cache.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous logout operation.</returns>
    Task LogoutAsync(string? userPrincipalName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a token silently for the specified scopes and tenant.
    /// </summary>
    /// <param name="scopes">The scopes for which the token is requested.</param>
    /// <param name="tenantId">The tenant ID for which the token is requested.</param>
    /// <param name="promptOnError">Whether to prompt the user on error.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="profile">
    /// The resolved profile for the current invocation.
    /// profile will be set from the engine, fallback for other direct caller like TUI/Tooling
    /// </param>
    /// <returns>The result of the token acquisition attempt.</returns>
    Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default, Profile? profile = null);
}
