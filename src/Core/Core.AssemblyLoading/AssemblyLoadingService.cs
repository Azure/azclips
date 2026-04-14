using System.Reflection;
using Core.Abstractions;

namespace Core.AssemblyLoading
{
    public class AssemblyLoadingService(ClientType clientType, IEnumerable<IExtensionLoader> extensionLoaders, ILoggingService loggingService): IAssemblyLoadingService
    {
        private Assembly? LoadExtension(IEnumerable<IExtensionLoader> extensionLoaders, string extensionName, string? version = null) => extensionLoaders
            .Select(loader => loader.LoadExtension(extensionName, version))
            .FirstOrDefault(assembly => assembly != null);

        //TODO(Yabo): Properly handle ALC, for now only CLI use ALC but create new ALC on every extension loading
        // this works for single command scenario but not optimal in interactive mode.
        public Assembly LoadExtension(string extensionName, string? extensionVersion = null)
        {
            switch (clientType)
            {
                case ClientType.CLI:
                    return LoadExtensionFromAssembly(extensionName, extensionVersion);
                case ClientType.PowerShell:
                    return LoadExtensionFromLoaded(extensionName);
                default:
                    throw new ArgumentException($"Unsupported client type: {clientType}");
            } 
        }

        private Assembly LoadExtensionFromLoaded(string extensionName)
        {
            var assemblyName = Constants.cirrusPrefix + extensionName;

            try
            {
                return Assembly.Load(assemblyName);
            }
            catch (System.IO.FileNotFoundException ex)
            {
                throw new InvalidOperationException($"Extension '{assemblyName}' could not be found by any extension loader.", ex);
            }
            catch (System.BadImageFormatException ex)
            {
                throw new InvalidOperationException($"Extension '{assemblyName}' could not be found by any extension loader.", ex);
            }
        }
        
        private Assembly LoadExtensionFromAssembly(string extensionName, string? extensionVersion)
        {
            loggingService.Info($"Processing command with extension '{Constants.cirrusPrefix + extensionName}'.");
            return LoadExtension(extensionLoaders, extensionName, extensionVersion) ?? throw new InvalidOperationException($"Extension '{Constants.cirrusPrefix + extensionName}' could not be found by any extension loader.");
        }
    }
}