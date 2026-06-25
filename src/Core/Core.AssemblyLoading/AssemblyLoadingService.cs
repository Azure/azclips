// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.InteropServices;
using Core.Abstractions;

namespace Core.AssemblyLoading
{
    public class AssemblyLoadingService(IAssemblyLoadingProvider provider): IAssemblyLoadingService
    {
        //TODO(Yabo): Properly handle ALC, for now only CLI use ALC but create new ALC on every extension loading
        // this works for single command scenario but not optimal in interactive mode.
        // Assembly loading service should implement async
        public Assembly LoadExtension(string extensionName, string? extensionVersion = null)
        {
            return provider.LoadExtension(extensionName, extensionVersion);
        }

        public IList<Tuple<string, string, Func<Assembly>>>? LoadAllExtensions()
        {
            IList<Tuple<string, string?>>? extensionNames = provider.GetAllInstalledExtensions();
            if (extensionNames == null || extensionNames.Count == 0)
            {
                return null;
            }

            var assemblies = new List<Tuple<string, string, Func<Assembly>>>();
            foreach (var extensionName in extensionNames)
            {
                var assembly = () => provider.LoadExtension(extensionName.Item1, extensionName.Item2);
                assemblies.Add(Tuple.Create(extensionName.Item1, extensionName.Item2 ?? string.Empty, assembly));
            }
            return assemblies;
        }
    }
}