using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Processors
{
    public class ProfileShowProcessor : LoginProcessorBase
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Name of the profile to show. Shows active profile if omitted.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        public override async Task ProcessAsync()
        {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            Profile? profile;
            if (!string.IsNullOrEmpty(Name))
            {
                profile = await profileService.GetProfileAsync(Name);
                if (profile is null)
                {
                    var all = await profileService.GetAllProfilesAsync();
                    var available = string.Join(", ", all.Select(p => p.Name));
                    await WriteOutput($"Error: Profile '{Name}' not found. Available profiles: {(string.IsNullOrEmpty(available) ? "(none)" : available)}.");
                    return;
                }
            }
            else
            {
                profile = await profileService.GetActiveProfileAsync();
                if (profile is null)
                {
                    await WriteOutput("No active profile. Run 'login --out-profile <name>' to create one.");
                    return;
                }
            }

            var output = $"Name:              {profile.Name}\n" +
                         $"Tenant ID:         {profile.TenantId}\n" +
                         $"Subscription ID:   {profile.SubscriptionId}\n" +
                         $"Cloud:             {profile.CloudName}\n" +
                         $"Auth Method:       {profile.AuthMethod}\n" +
                         $"User:              {profile.UserPrincipalName ?? "(none)"}\n" +
                         $"Client ID:         {profile.ClientId ?? "(none)"}";

            if (profile.Defaults.Count > 0)
            {
                output += "\nDefaults:";
                foreach (var kvp in profile.Defaults)
                    output += $"\n  {kvp.Key}: {kvp.Value}";
            }

            await WriteOutput(output);
            await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
        }
    }
}
