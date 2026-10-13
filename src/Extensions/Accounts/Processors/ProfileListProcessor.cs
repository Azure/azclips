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
            var results = profiles
                .Select(profile => ProfileResult.From(
                    profile,
                    isActive: string.Equals(profile.Name, active, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            await WriteOutput(results);

            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
