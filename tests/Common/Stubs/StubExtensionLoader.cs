// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Core.Abstractions;

namespace Tests.Common.Stubs;

public class StubExtensionLoader(Assembly extensionAssembly, string extensionName) : IAssemblyLoadingProvider
{
    public IList<Tuple<string, string?>>? GetAllInstalledExtensions() => throw new NotImplementedException();

    public Assembly LoadExtension(string requestedExtensionName, string? version = null)
    {
        if (string.Equals(requestedExtensionName, extensionName, StringComparison.OrdinalIgnoreCase))
        {
            return extensionAssembly;
        }

        throw new InvalidOperationException($"Extension '{requestedExtensionName}' is not available in stub provider.");
    }
}