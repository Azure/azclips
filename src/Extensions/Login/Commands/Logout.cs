using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    [CommandInfo(Name = "logout", Description = "Log out of Azure.")]
    public class LogoutCommand : LoginCommandBase
    {
        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(LogoutCommand)}.{nameof(ProcessAsync)}");
            var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
            var telemetryService = serviceProvider.GetRequiredService<ITelemetryService>();
            var authService = serviceProvider.GetRequiredService<IAuthService>();
            var authContextService = serviceProvider.GetRequiredService<IAuthContextService>();
            authService.LogoutAsync().GetAwaiter().GetResult();
            authContextService.ClearContextAsync().GetAwaiter().GetResult();
            await WriteOutput(true);
        }
    }
}
