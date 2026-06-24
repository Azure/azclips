// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli;

using System.Reflection;
using Core.Abstractions;

public class CLIAssemblyLoadingProvider(IEnumerable<IExtensionLoader> extensionLoaders, ILoggingService loggingService) : IAssemblyLoadingProvider
{
    private Assembly? LoadExtension(IEnumerable<IExtensionLoader> extensionLoaders, string extensionName, string? version = null) => extensionLoaders
                    .Select(loader => loader.LoadExtension(extensionName, version))
                    .FirstOrDefault(assembly => assembly != null);

    public Assembly LoadExtension(string extensionName, string? extensionVersion = null)
    {
        loggingService.Info($"Processing command with extension '{Constants.cirrusPrefix + extensionName}'.");
        return LoadExtension(extensionLoaders, extensionName, extensionVersion) ?? throw new InvalidOperationException($"Extension '{Constants.cirrusPrefix + extensionName}' could not be found by any extension loader.");
    }

    public IList<Tuple<string, string?>>? GetAllInstalledExtensions() => throw new NotImplementedException();
}