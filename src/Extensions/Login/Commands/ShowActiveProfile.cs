using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class ShowActiveProfileCommand : LoginCommandBase
    {
        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(ShowActiveProfileCommand)}.{nameof(ProcessAsync)}");
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            var active = authContextService.GetActiveProfileAsync().GetAwaiter().GetResult();

            if (active is null)
            {
                throw new InvalidOperationException("No active profile is set.");
            }

            await WriteOutput(active);
        }
    }
}