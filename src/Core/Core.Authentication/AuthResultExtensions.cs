// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Msal = Microsoft.Identity.Client;

namespace Core.Authentication;

internal static class AuthResultExtensions
{
    public static AuthResult ToAuthResult(this Msal.AuthenticationResult msalAuthResult)
    {
        return new AuthResult
        {
            AccessToken = msalAuthResult.AccessToken,
            ExpiresOn = msalAuthResult.ExpiresOn,
            Scopes = [.. msalAuthResult.Scopes], // create a copy of the scopes array to not reference MSAL's internal array
            TenantId = msalAuthResult.TenantId,
            CorrelationId = msalAuthResult.CorrelationId,
            UserPrincipalName = msalAuthResult.Account?.Username,
            EntraObjectId = msalAuthResult.UniqueId,
        };
    }
}