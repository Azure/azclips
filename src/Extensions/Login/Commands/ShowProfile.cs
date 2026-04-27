using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class ShowProfileCommand : LoginCommandBase
    {
        [ArgumentInfo(
            Required = false,
            Description = "Name of the profile to show. Defaults to the active profile.",
            SerializedName = "Name")]
        public string Name { get; set; } = "";

        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(ShowProfileCommand)}.{nameof(ProcessAsync)}");
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            Profile? profile;
            if (string.IsNullOrEmpty(Name))
            {
                profile = await profileService.GetActiveProfileAsync();
                if (profile is null)
                {
                    await WriteOutput("No active profile. Run 'azv2 login --subscription-id <id>' or 'azv2 profile switch <name>' to set one.");
                    return;
                }
            }
            else
            {
                profile = await profileService.GetProfileAsync(Name);
                if (profile is null)
                {
                    var all = await profileService.GetAllProfilesAsync();
                    var names = string.Join(", ", all.Select(p => p.Name));
                    await WriteOutput($"Profile '{Name}' not found. Available profiles: {(string.IsNullOrEmpty(names) ? "(none)" : names)}");
                    return;
                }
            }

            await WriteOutput(profile);
        }
    }
}
