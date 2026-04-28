using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Processors
{
    public class ProfileModifyProcessor : LoginProcessorBase
    {
        [ArgumentInfo(
        Required = true,
        Description = @"Name of the profile to modify.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"New subscription ID.",
        SerializedName = @"Subscription")]
        public string Subscription { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"New tenant ID.",
        SerializedName = @"TenantId")]
        public string TenantId { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Set a default value (key=value).",
        SerializedName = @"Default")]
        public string Default { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Remove a default by key.",
        SerializedName = @"RemoveDefault")]
        public string RemoveDefault { get; set; } = "";

        public override async Task ProcessAsync()
        {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            bool hasChanges = !string.IsNullOrEmpty(Subscription) ||
                              !string.IsNullOrEmpty(TenantId) ||
                              !string.IsNullOrEmpty(Default) ||
                              !string.IsNullOrEmpty(RemoveDefault);

            if (!hasChanges)
            {
                await WriteOutput("Error: No modification flags provided. Use --subscription, --tenant-id, --default key=value, or --remove-default key.");
                return;
            }

            try
            {
                await profileService.UpdateProfileAsync(Name, profile =>
                {
                    if (!string.IsNullOrEmpty(Subscription))
                        profile.SubscriptionId = Subscription;
                    if (!string.IsNullOrEmpty(TenantId))
                        profile.TenantId = TenantId;
                    if (!string.IsNullOrEmpty(Default) && Default.Contains('='))
                    {
                        var parts = Default.Split('=', 2);
                        profile.Defaults[parts[0]] = parts[1];
                    }
                    if (!string.IsNullOrEmpty(RemoveDefault))
                        profile.Defaults.Remove(RemoveDefault);
                });

                var updated = await profileService.GetProfileAsync(Name);
                await WriteOutput($"Profile '{Name}' updated.");
            }
            catch (InvalidOperationException ex)
            {
                await WriteOutput($"Error: {ex.Message}");
            }

            await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
        }
    }
}
