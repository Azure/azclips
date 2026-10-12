// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Result of an authentication operation.
/// This is a simplified version of <see cref="Microsoft.Identity.Client.AuthenticationResult"/> for convenience.
/// But it's meant to be independent of MSAL.
/// </summary>
public record AuthResult
{
    /// <summary>
    /// Access token that can be used as a bearer token to access protected web APIs.
    /// </summary>
    public required string AccessToken;

    /// <summary>
    /// The point in time when the access token expires.
    /// </summary>
    public required DateTimeOffset ExpiresOn;

    /// <summary>
    /// The ID token, if returned by the service.
    /// </summary>
    // public string? IdToken => Result.IdToken;

    /// <summary>
    /// The granted scope values returned by the service.
    /// </summary>
    public required IEnumerable<string> Scopes;

    /// <summary>
    /// The account information from the authentication result.
    /// </summary>
    // public IAccount Account => Result.Account;

    /// <summary>
    /// The unique ID of the account (oid or sub claim from the ID token).
    /// </summary>
    // public string? UniqueId => Result.UniqueId;

    /// <summary>
    /// The tenant ID from which the token was acquired.
    /// </summary>
    public required string TenantId;

    /// <summary>
    /// The authenticated account's user principal name (from MSAL <c>IAccount.Username</c>), if available.
    /// </summary>
    public string? UserPrincipalName;

    /// <summary>
    /// The authenticated user's Entra object ID, if available from the authentication result.
    /// </summary>
    public string? EntraObjectId;

    /// <summary>
    /// The correlation ID used for the authentication request.
    /// Null for credential types that do not provide a correlation ID (e.g., MI/SP).
    /// </summary>
    public Guid? CorrelationId;

    /// <summary>
    /// The token type (e.g., "Bearer").
    /// </summary>
    // public required string TokenType;

    /// <summary>
    /// Metadata about the authentication result (token source, duration, etc.).
    /// </summary>
    // public AuthenticationResultMetadata AuthenticationResultMetadata => Result.AuthenticationResultMetadata;
}
