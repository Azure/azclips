using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class LoginCommand : LoginCommandBase
    {
        [ArgumentInfo(
        Required = true,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string SubscriptionId { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Tenant ID to login to.",
        SerializedName = @"TenantId")]
        public string TenantId { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Use device code flow for authentication.",
        SerializedName = @"UseDeviceCode")]
        public bool UseDeviceCode { get; set; } = false;

        [ArgumentInfo(
        Required = false,
        Description = @"Save login result as a named profile. If omitted, creates/overwrites the 'default' profile.",
        SerializedName = @"OutProfile")]
        public string OutProfile { get; set; } = "";

        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(LoginCommand)}.{nameof(ProcessAsync)}");
            var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
            var authService = CirrusServiceProvider.GetRequiredService<IAuthService>();
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            LoginRequest loginRequest = new()
            {
                TenantId = TenantId,
                SubscriptionId = SubscriptionId,
                UseDeviceCodeFlow = UseDeviceCode
            };

            var authResult = authService.LoginAsync(loginRequest).GetAwaiter().GetResult();
            AuthContext authContext = new AuthContext(TenantId: authResult.TenantId, SubscriptionId: SubscriptionId);

            // Determine profile name: explicit --out-profile or auto-"default"
            var profileName = string.IsNullOrEmpty(OutProfile) ? "default" : OutProfile;

            // Warn if overwriting an existing profile
            var existing = profileService.GetProfileAsync(profileName).GetAwaiter().GetResult();
            if (existing is not null && !string.IsNullOrEmpty(OutProfile))
            {
                await WriteOutput($"Warning: Profile '{profileName}' already exists and will be overwritten.");
            }

            // Create and save profile
            var profile = new Profile
            {
                Name = profileName,
                TenantId = authResult.TenantId,
                SubscriptionId = SubscriptionId,
                AuthMethod = UseDeviceCode ? "device-code" : "interactive",
            };
            profileService.SaveProfileAsync(profile).GetAwaiter().GetResult();
            profileService.SetActiveProfileAsync(profileName).GetAwaiter().GetResult();

            // Backward compatibility: also save to context.json via adapter
            authContextService.SaveContextAsync(authContext).GetAwaiter().GetResult();
            await WriteOutput(authContext);
        }
    }
}
