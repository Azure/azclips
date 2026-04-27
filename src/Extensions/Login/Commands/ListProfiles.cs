using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class ListProfilesCommand : LoginCommandBase
    {
        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(ListProfilesCommand)}.{nameof(ProcessAsync)}");
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            var profiles = authContextService.ListProfilesAsync().GetAwaiter().GetResult();
            var active = authContextService.GetActiveProfileAsync().GetAwaiter().GetResult();

            var output = profiles
                .Select(p => new
                {
                    p.ProfileName,
                    p.TenantId,
                    p.SubscriptionId,
                    IsActive = string.Equals(active?.ProfileName, p.ProfileName, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();

            foreach (var row in output)
            {
                await WriteOutput(row);
            }
        }
    }
}