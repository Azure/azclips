using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class LogoutCommand : LoginCommandBase
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Name of the profile to log out. Defaults to the active profile.",
        SerializedName = @"Profile")]
        public string Profile { get; set; } = "";

        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(LogoutCommand)}.{nameof(ProcessAsync)}");
            var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
            var authService = CirrusServiceProvider.GetRequiredService<IAuthService>();
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            // If --profile is specified, verify it exists
            if (!string.IsNullOrEmpty(Profile))
            {
                var profile = profileService.GetProfileAsync(Profile).GetAwaiter().GetResult();
                if (profile is null)
                {
                    await WriteOutput($"Profile '{Profile}' not found.");
                    return;
                }
            }

            // Clear MSAL tokens (tokens only, not profile definitions)
            authService.LogoutAsync().GetAwaiter().GetResult();
            await WriteOutput(true);
        }
    }
}
