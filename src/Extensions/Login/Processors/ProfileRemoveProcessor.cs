using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Login.Processors
{
    /// <summary>
    /// Deletes a profile definition. If the deleted profile was active, the active pointer is cleared
    /// (no fallback to "default").
    /// </summary>
    public class ProfileRemoveProcessor : ProfileProcessorBase
    {
        [ArgumentInfo(
        Required = true,
        Description = @"The name of the profile to remove.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();

            bool removed = await profileService.RemoveProfileAsync(Name);
            if (!removed)
            {
                throw new ArgumentException($"Profile '{Name}' does not exist.");
            }

            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
