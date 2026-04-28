using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Processors
{
    public class LogoutProcessor : LoginProcessorBase
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Name of the profile to logout from. Uses active profile if omitted.",
        SerializedName = @"TargetProfile")]
        public string TargetProfile { get; set; } = "";

        public override async Task ProcessAsync()
        {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            var authService = CirrusServiceProvider.GetRequiredService<IAuthService>();
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            Profile? profile;
            if (!string.IsNullOrEmpty(TargetProfile))
            {
                profile = await profileService.GetProfileAsync(TargetProfile);
                if (profile is null)
                {
                    var all = await profileService.GetAllProfilesAsync();
                    var available = string.Join(", ", all.Select(p => p.Name));
                    await WriteOutput($"Error: Profile '{TargetProfile}' not found. Available profiles: {(string.IsNullOrEmpty(available) ? "(none)" : available)}.");
                    return;
                }
            }
            else
            {
                profile = await profileService.GetActiveProfileAsync();
                if (profile is null)
                {
                    await WriteOutput("Error: No active profile. Run 'login --out-profile <name>' to create one, or 'profile switch <name>' to select one.");
                    return;
                }
            }

            authService.LogoutAsync().GetAwaiter().GetResult();
            await WriteOutput($"Logged out from profile: {profile.Name}");
            await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
        }
    }
}
