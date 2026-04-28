using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Processors
{
    public class LoginProcessor : LoginProcessorBase
    {
        [ArgumentInfo(
        Required = false,
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
        Description = @"Name of the profile to create or overwrite. Defaults to 'temp'.",
        SerializedName = @"OutProfile")]
        public string OutProfile { get; set; } = "";

        public override async Task ProcessAsync()
        {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            var authService = CirrusServiceProvider.GetRequiredService<IAuthService>();
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            var profileName = string.IsNullOrEmpty(OutProfile) ? "temp" : OutProfile;

            if (!Profile.IsValidName(profileName))
            {
                await WriteOutput($"Error: Invalid profile name '{profileName}'. Profile names must match [a-zA-Z0-9_-]+ (alphanumeric, hyphens, underscores only).");
                return;
            }

            // Check for overwrite
            var existing = await profileService.GetProfileAsync(profileName);
            if (existing is not null)
            {
                await WriteOutput($"Warning: Profile '{profileName}' already exists and will be overwritten.");
            }

            LoginRequest loginRequest = new()
            {
                TenantId = TenantId,
                SubscriptionId = SubscriptionId,
                UseDeviceCodeFlow = UseDeviceCode
            };

            var authResult = authService.LoginAsync(loginRequest).GetAwaiter().GetResult();

            var profile = new Profile
            {
                Name = profileName,
                TenantId = authResult.TenantId,
                SubscriptionId = string.IsNullOrEmpty(SubscriptionId) ? authResult.TenantId : SubscriptionId,
                AuthMethod = UseDeviceCode ? "device-code" : "browser",
                UserPrincipalName = null,
                CloudName = "AzureCloud"
            };

            await profileService.SaveProfileAsync(profile, setActive: true);

            AuthContext authContext = new AuthContext(TenantId: profile.TenantId, SubscriptionId: profile.SubscriptionId);
            await WriteOutput(authContext);
            await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
        }
    }
}
