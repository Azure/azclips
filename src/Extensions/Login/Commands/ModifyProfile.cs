using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class ModifyProfileCommand : LoginCommandBase
    {
        [ArgumentInfo(
            Required = true,
            Description = "Name of the profile to modify.",
            SerializedName = "Name")]
        public string Name { get; set; } = "";

        [ArgumentInfo(
            Required = false,
            Description = "New subscription ID.",
            SerializedName = "SubscriptionId")]
        public string SubscriptionId { get; set; } = "";

        [ArgumentInfo(
            Required = false,
            Description = "New tenant ID.",
            SerializedName = "TenantId")]
        public string TenantId { get; set; } = "";

        [ArgumentInfo(
            Required = false,
            Description = "Set a default value (key=value format).",
            SerializedName = "Set")]
        public string Set { get; set; } = "";

        [ArgumentInfo(
            Required = false,
            Description = "Remove a default by key.",
            SerializedName = "RemoveDefault")]
        public string RemoveDefault { get; set; } = "";

        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(ModifyProfileCommand)}.{nameof(ProcessAsync)}");
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            var profile = await profileService.GetProfileAsync(Name);
            if (profile is null)
            {
                await WriteOutput($"Profile '{Name}' not found.");
                return;
            }

            if (!string.IsNullOrEmpty(SubscriptionId))
                profile.SubscriptionId = SubscriptionId;
            if (!string.IsNullOrEmpty(TenantId))
                profile.TenantId = TenantId;
            if (!string.IsNullOrEmpty(Set))
            {
                var eqIndex = Set.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = Set[..eqIndex].Trim();
                    var value = Set[(eqIndex + 1)..].Trim();
                    profile.Defaults[key] = value;
                }
            }
            if (!string.IsNullOrEmpty(RemoveDefault))
                profile.Defaults.Remove(RemoveDefault);

            await profileService.SaveProfileAsync(profile);
            await WriteOutput(profile);
        }
    }
}
