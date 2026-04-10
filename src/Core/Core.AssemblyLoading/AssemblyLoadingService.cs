using System.Reflection;
using Core.Abstractions;

namespace Core.AssemblyLoading
{
    public class AssemblyLoadingService(IEnumerable<IExtensionLoader> extensionLoaders, ILoggingService loggingService): IAssemblyLoadingService
    {
        private Assembly? LoadExtension(IEnumerable<IExtensionLoader> extensionLoaders, string extensionFolderName, string extensionName, string? version = null) => extensionLoaders
            .Select(loader => loader.LoadExtension(extensionFolderName, extensionName, version))
            .FirstOrDefault(assembly => assembly != null);

        //TODO(Yabo): Properly handle ALC, for now only CLI use ALC but create new ALC on every extension loading
        // this works for single command scenario but not optimal in interactive mode.
        public Assembly LoadExtension(string extensionName, ClientType clientType, string? extensionVersion = null)
        {
            extensionName = Constants._prefix + extensionName;
            switch (clientType)
            {
                case ClientType.CLI:
                    return LoadExtension(extensionName, extensionVersion);
                case ClientType.PowerShell:
                    return LoadExtension(extensionName);
                default:
                    throw new ArgumentException($"Unsupported client type: {clientType}");
            } 
        }

        private Assembly LoadExtension(string extensionName)
        {
            return Assembly.Load(extensionName) ?? throw new InvalidOperationException($"Extension '{extensionName}' could not be found by any extension loader.");
        }
        
        private Assembly LoadExtension(string extensionName, string? extensionVersion)
        {
            string extensionFolderName = Constants.extensionFolderPrefix + extensionName;
            loggingService.Info($"Processing command with extension '{extensionName}'.");
            return LoadExtension(extensionLoaders, extensionFolderName, extensionName, extensionVersion) ?? throw new InvalidOperationException($"Extension '{extensionName}' could not be found by any extension loader.");
        }
    }
}