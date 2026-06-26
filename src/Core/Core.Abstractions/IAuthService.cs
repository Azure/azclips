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
    /// Logs out the current user. Tokens and cached credentials will be cleared.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous logout operation.</returns>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a token silently for the specified scopes and tenant.
    /// </summary>
    /// <param name="scopes">The scopes for which the token is requested.</param>
    /// <param name="tenantId">The tenant ID for which the token is requested.</param>
    /// <param name="promptOnError">Whether to prompt the user on error.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The result of the token acquisition attempt.</returns>
    Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default);
}
