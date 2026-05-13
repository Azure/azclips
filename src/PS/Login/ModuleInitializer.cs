using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Cmdlets.Login;

public class ModuleInitializer : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    public void OnImport()
    {
        CirrusServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
        });
        CirrusServiceProvider.Initialize(ClientType.PowerShell);
    }

    public void OnRemove(PSModuleInfo module)
    {
        CirrusServiceProvider.Dispose();
    }
}