// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IdentityModel.Tokens.Jwt;
using Core.Abstractions;

namespace Core.Authentication;

/// <summary>
/// Identity information carried by an access token. Managed identity endpoints return only a raw
/// access token (no account or id token), so the issued token itself is the only source for the
/// tenant and the identity that was authenticated.
/// </summary>
public readonly record struct AccessTokenClaims(string? TenantId, string? ClientId, string? ObjectId)
{
    private const string TenantIdClaim = "tid";
    private const string AppIdClaim = "appid";      // v1.0 tokens
    private const string AuthorizedPartyClaim = "azp"; // v2.0 tokens
    private const string ObjectIdClaim = "oid";

    /// <summary>
    /// Reads the identity claims from a JWT access token without validating its signature: the token
    /// was just obtained from the identity endpoint over a trusted channel and is only inspected to
    /// describe the sign-in. Returns empty claims when the token is absent or not a readable JWT, so
    /// a login never fails because of a token shape we cannot parse.
    /// </summary>
    public static AccessTokenClaims Read(string? accessToken, ILoggingService? logger = null)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return default;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(accessToken))
            {
                return default;
            }

            var token = handler.ReadJwtToken(accessToken);
            string? Claim(string type) =>
                token.Claims.FirstOrDefault(c => string.Equals(c.Type, type, StringComparison.Ordinal))?.Value is { Length: > 0 } value
                    ? value
                    : null;

            return new AccessTokenClaims(
                TenantId: Claim(TenantIdClaim),
                ClientId: Claim(AppIdClaim) ?? Claim(AuthorizedPartyClaim),
                ObjectId: Claim(ObjectIdClaim));
        }
        catch (Exception ex)
        {
            logger?.Debug($"[{nameof(AccessTokenClaims)}] Could not read the access token claims: {ex.Message}");
            return default;
        }
    }
}
