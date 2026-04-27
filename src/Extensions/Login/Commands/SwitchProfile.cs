using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class SwitchProfileCommand : LoginCommandBase
    {
        [ArgumentInfo(
            Required = true,
            Description = "Name of the profile to switch to.",
            SerializedName = "Name")]
        public string Name { get; set; } = "";

        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(SwitchProfileCommand)}.{nameof(ProcessAsync)}");
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            try
            {
                await profileService.SetActiveProfileAsync(Name);
                await WriteOutput($"Switched to profile '{Name}'.");
            }
            catch (InvalidOperationException ex)
            {
                await WriteOutput(ex.Message);
            }
        }
    }
}
