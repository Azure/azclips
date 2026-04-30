using System.Reflection;
using Core.Abstractions;

namespace Core.AssemblyLoading
{
    public class AssemblyLoadingService(IAssemblyLoadingProvider provider): IAssemblyLoadingService
    {
        //TODO(Yabo): Properly handle ALC, for now only CLI use ALC but create new ALC on every extension loading
        // this works for single command scenario but not optimal in interactive mode.
        public Assembly LoadExtension(string extensionName, string? extensionVersion = null)
        {
            return provider.LoadExtension(extensionName, extensionVersion);
        }
    }
}