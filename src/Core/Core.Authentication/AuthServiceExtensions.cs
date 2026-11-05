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
        return new ProfileCredential(authService, profile);
    }

    private sealed class ProfileCredential(IAuthService authService, Profile profile) : IProfileCredential
    {
        public Profile Profile { get; } = profile;

        public Task<AuthResult> GetTokenAsync(string[] scopes, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(scopes);
            cancellationToken.ThrowIfCancellationRequested();
            return authService.AcquireTokenSilentlyAsync([.. scopes], Profile.TenantId,
                promptOnError: false, cancellationToken: cancellationToken, profile: Profile);
        }
    }
}
