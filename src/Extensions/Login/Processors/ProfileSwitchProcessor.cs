using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Processors
{
    public class ProfileSwitchProcessor : LoginProcessorBase
    {
        [ArgumentInfo(
        Required = true,
        Description = @"Name of the profile to set as active.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        public override async Task ProcessAsync()
        {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            try
            {
                await profileService.SetActiveProfileAsync(Name);
                await WriteOutput($"Switched to profile: {Name}");
            }
            catch (InvalidOperationException ex)
            {
                await WriteOutput($"Error: {ex.Message}");
            }

            await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
        }
    }
}
