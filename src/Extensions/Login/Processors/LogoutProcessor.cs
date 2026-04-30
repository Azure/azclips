using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Processors
{
    public class LogoutProcessor : LoginProcessorBase
    {
        public override async Task ProcessAsync()
        {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
            var authService = CirrusServiceProvider.GetRequiredService<IAuthService>();
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            authService.LogoutAsync().GetAwaiter().GetResult();
            authContextService.ClearContextAsync().GetAwaiter().GetResult();
            await WriteOutput(true);
            await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
        }
    }
}
