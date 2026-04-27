using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class RemoveProfileCommand : LoginCommandBase
    {
        [ArgumentInfo(
            Required = true,
            Description = "Name of the profile to remove.",
            SerializedName = "Name")]
        public string Name { get; set; } = "";

        [ArgumentInfo(
            Required = false,
            Description = "Skip confirmation prompt.",
            SerializedName = "Yes")]
        public bool Yes { get; set; } = false;

        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(RemoveProfileCommand)}.{nameof(ProcessAsync)}");
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            var removed = await profileService.RemoveProfileAsync(Name);
            if (!removed)
            {
                await WriteOutput($"Profile '{Name}' not found.");
                return;
            }

            await WriteOutput($"Profile '{Name}' removed.");
        }
    }
}
