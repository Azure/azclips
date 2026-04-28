using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Processors
{
    public class ProfileListProcessor : LoginProcessorBase
    {
        public override async Task ProcessAsync()
        {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            var profileService = CirrusServiceProvider.GetRequiredService<IProfileService>();

            var profiles = await profileService.GetAllProfilesAsync();
            if (profiles.Count == 0)
            {
                await WriteOutput("No profiles configured. Run 'login --out-profile <name>' to create one.");
                await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
                return;
            }

            var active = await profileService.GetActiveProfileAsync();
            var activeName = active?.Name;

            var header = $"  {"NAME",-12} {"TENANT",-38} {"SUBSCRIPTION",-38} {"AUTH"}";
            await WriteOutput(header);

            foreach (var p in profiles)
            {
                var marker = string.Equals(p.Name, activeName, StringComparison.OrdinalIgnoreCase) ? "*" : " ";
                var line = $"{marker} {p.Name,-12} {p.TenantId,-38} {p.SubscriptionId,-38} {p.AuthMethod}";
                await WriteOutput(line);
            }

            await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
        }
    }
}
