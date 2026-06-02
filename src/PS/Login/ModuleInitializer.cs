using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;
using Microsoft.Azure.Cirrus.Cmdlets.Login.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Cmdlets.Login;

public class ModuleInitializer : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    // private static ITelemetryHost? _telemetryHost;
    private static readonly object _hostLock = new();

    public void OnImport()
    {
        CirrusServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
            services.AddSingleton<ITelemetryProvider, PSTelemetryProvider>();

        });
        CirrusServiceProvider.Initialize(ClientType.PowerShell);
    }

    public void OnRemove(PSModuleInfo module)
    {
        CirrusServiceProvider.Dispose();
    }
}
