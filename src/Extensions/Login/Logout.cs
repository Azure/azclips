using Core.Abstractions;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class LogoutCommand : IProcessor
    {
        public object? Process()
        {
            var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
            var telemetryService = serviceProvider.GetRequiredService<ITelemetryService>();
            using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(LogoutCommand)}.{nameof(Process)}");

            var authService = serviceProvider.GetRequiredService<IAuthService>();
            var authContextService = serviceProvider.GetRequiredService<IAuthContextService>();
            var authContext = authContextService.GetCurrentContextAsync().GetAwaiter().GetResult();
            authService.LogoutAsync().GetAwaiter().GetResult();
            authContextService.ClearContextAsync().GetAwaiter().GetResult();
            return null;
        }

        public string Process(bool Serialize)
        {
            Process();
            return string.Empty;
        }
    }
}
