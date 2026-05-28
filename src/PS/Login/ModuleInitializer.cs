using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Cirrus.Cmdlets.Login.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Cmdlets.Login;

public class ModuleInitializer : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    private static ITelemetryHost? _telemetryHost;
    private static readonly object _hostLock = new();

    public void OnImport()
    {
        CirrusServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
        });
        CirrusServiceProvider.Initialize(ClientType.PowerShell);

        lock (_hostLock)
        {
            if (_telemetryHost is not null)
            {
                return;
            }

            var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
            var telemetryOptions = CirrusServiceProvider.GetRequiredService<IConfigurationService>()
                .GetConfiguration<TelemetryOptions>();
            _telemetryHost = PsTelemetryHost.CreateBuilder(telemetryService, telemetryOptions)
                .UseDirectSender()
                .Build();
        }
    }

    public void OnRemove(PSModuleInfo module)
    {
        lock (_hostLock)
        {
            _telemetryHost?.DisposeAsync().GetAwaiter().GetResult();
            _telemetryHost = null;
        }

        CirrusServiceProvider.Dispose();
    }
}
