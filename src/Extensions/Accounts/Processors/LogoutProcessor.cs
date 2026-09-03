// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Processors
{
    /// <summary>
    /// Signs out the active profile: removes the profile's account from the shared MSAL token cache and
    /// clears the active-profile pointer. When no profile is active, logout is a no-op.
    /// </summary>
    public class LogoutProcessor : LoginProcessorBase
    {
        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();
            Profile? activeProfile = await profileService.GetActiveProfileAsync();

            if (activeProfile is not null)
            {
                // 1. Remove the active profile's account from the shared MSAL token cache. When the
                //    profile has no recorded user principal name, no account can be identified.
                var authService = AzclipsServiceProvider.GetRequiredService<IAuthService>();
                await authService.LogoutAsync(activeProfile.UserPrincipalName);

                // 2. Unset the active-profile pointer.
                await profileService.SetActiveProfileNameAsync(null);
            }

            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
