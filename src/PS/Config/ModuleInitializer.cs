using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Cmdlets.Config;

public class ModuleInitializer : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    public void OnImport()
    {
        CirrusServiceProvider.Initialize(ClientType.PowerShell);
    }

    public void OnRemove(PSModuleInfo module)
    {
        CirrusServiceProvider.Dispose();
    }
}
