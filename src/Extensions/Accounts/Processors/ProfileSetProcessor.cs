using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts.Models;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Processors
{
    /// <summary>Sets the active profile persistently. Validates existence; performs no re-authentication.</summary>
    public class ProfileSetProcessor : ProfileProcessorBase
    {
        [ArgumentInfo(
        Required = true,
        Description = @"The name of the profile to activate.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();

            Profile profile = await profileService.GetProfileAsync(Name)
                ?? throw AzclipsErrorException.ProfileNotFound($"Profile '{Name}' does not exist.");

            await profileService.SetActiveProfileNameAsync(profile.Name);
            await WriteOutput(ProfileResult.From(profile, isActive: true));
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
