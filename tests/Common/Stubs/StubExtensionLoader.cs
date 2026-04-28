// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Core.AssemblyLoading;

namespace Tests.Common.Stubs;

public class StubExtensionLoader(Assembly extensionAssembly, string extensionName) : IExtensionLoader
{
    public Assembly? LoadExtension(string requestedExtensionName, string? version = null)
    {
        return string.Equals(requestedExtensionName, extensionName, StringComparison.OrdinalIgnoreCase)
            ? extensionAssembly
            : null;
    }
}