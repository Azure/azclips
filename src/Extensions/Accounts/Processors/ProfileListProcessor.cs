using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts.Models;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Processors
{
    /// <summary>Lists all saved profiles, indicating which one is active.</summary>
    public class ProfileListProcessor : ProfileProcessorBase
    {
        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();

            var profiles = await profileService.GetAllProfilesAsync();
            var active = await profileService.GetActiveProfileNameAsync();

            var summaries = profiles
                .Select(profile => new ProfileSummary(
                    profile.Name,
                    string.Equals(profile.Name, active, StringComparison.OrdinalIgnoreCase),
                    profile.TenantId,
                    profile.SubscriptionId,
                    profile.CloudName,
                    profile.AuthMethod,
                    profile.UserPrincipalName))
                .ToList();

            await WriteOutput(summaries);

            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
