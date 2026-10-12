using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Processors
{
    /// <summary>Shows the full details of a single profile by name, or the active profile.</summary>
    public class ProfileShowProcessor : ProfileProcessorBase
    {
        [ArgumentInfo(
        Required = false,
        Description = @"The name of the profile to show.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Show the active profile. Cannot be used together with Name.",
        SerializedName = @"Active")]
        public bool Active { get; set; }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();

            bool hasName = !string.IsNullOrWhiteSpace(Name);
            if (Active && hasName)
            {
                throw new ArgumentException("Specify either a profile name or --active, not both.");
            }
            Profile profile = Active || !hasName
                ? await profileService.GetActiveProfileAsync()
                    ?? throw new InvalidOperationException("No active profile is set.")
                : await profileService.GetProfileAsync(Name)
                    ?? throw AzclipsErrorException.ProfileNotFound($"Profile '{Name}' does not exist.");

            await WriteOutput(profile);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
