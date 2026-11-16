// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;

namespace Core.AssemblyLoading
{
    public class AssemblyLoadingService(
        IAssemblyLoadingProvider provider,
        IProductVersionProvider versionProvider) : IAssemblyLoadingService
    {
        public string GetProductVersion() => versionProvider.ProductVersion;

        //TODO(Yabo): Properly handle ALC, for now only CLI use ALC but create new ALC on every extension loading
        // this works for single command scenario but not optimal in interactive mode.
        // Assembly loading service should implement async
        public Assembly LoadExtension(string extensionName, string? extensionVersion = null)
        {
            return provider.LoadExtension(extensionName, extensionVersion);
        }

        public IList<(InstalledExtensionRef Info, Func<Assembly> Load)>? LoadAllExtensions()
        {
            var refs = provider.GetAllInstalledExtensions();
            if (refs == null || refs.Count == 0)
            {
                return null;
            }

            var result = new List<(InstalledExtensionRef, Func<Assembly>)>(refs.Count);
            foreach (var r in refs)
            {
                var captured = r;
                result.Add((captured, () => provider.LoadExtension(captured.Name, captured.Version)));
            }
            return result;
        }
    }
}