using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class LogoutCommand : LoginCommandBase
    {
        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(LogoutCommand)}.{nameof(ProcessAsync)}");
            var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
            var authService = CirrusServiceProvider.GetRequiredService<IAuthService>();
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            authService.LogoutAsync().GetAwaiter().GetResult();
            authContextService.ClearContextAsync().GetAwaiter().GetResult();
            await WriteOutput(true);
        }
    }
}
