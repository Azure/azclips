using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Processors
{
    public class ProfileRemoveProcessor : LoginProcessorBase
    {
        [ArgumentInfo(
        Required = true,
        Description = @"Name of the profile to remove.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        public override async Task ProcessAsync()
        {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            try
            {
                var active = await profileService.GetActiveProfileAsync();
                bool wasActive = active is not null && string.Equals(active.Name, Name, StringComparison.OrdinalIgnoreCase);

                await profileService.DeleteProfileAsync(Name);

                if (wasActive)
                    await WriteOutput($"Warning: Removed active profile '{Name}'. No active profile set. Run 'profile switch <name>' to select one.");
                else
                    await WriteOutput($"Removed profile: {Name}");
            }
            catch (InvalidOperationException ex)
            {
                await WriteOutput($"Error: {ex.Message}");
            }

            await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
        }
    }
}
