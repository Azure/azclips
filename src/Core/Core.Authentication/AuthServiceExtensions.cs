// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Authentication;

public static class AuthServiceExtensions
{
    /// <summary>Binds an identity without acquiring a token or creating another token cache.</summary>
    public static IProfileCredential CreateCredential(this IAuthService authService, Profile profile)
    {
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(profile);
        return new ProfileCredential(profile, authService.AcquireTokenSilentlyAsync);
    }
}
