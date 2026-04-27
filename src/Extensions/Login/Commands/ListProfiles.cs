using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class ListProfilesCommand : LoginCommandBase
    {
        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(ListProfilesCommand)}.{nameof(ProcessAsync)}");
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            var profiles = await profileService.GetAllProfilesAsync();
            var activeProfile = await profileService.GetActiveProfileAsync();

            if (profiles.Count == 0)
            {
                await WriteOutput("No profiles found. Run 'azv2 login --subscription-id <id>' to create one.");
                return;
            }

            foreach (var profile in profiles)
            {
                var marker = activeProfile is not null &&
                    string.Equals(profile.Name, activeProfile.Name, StringComparison.OrdinalIgnoreCase)
                    ? "* " : "  ";
                await WriteOutput($"{marker}{profile.Name} (Tenant: {profile.TenantId}, Subscription: {profile.SubscriptionId})");
            }
        }
    }
}
