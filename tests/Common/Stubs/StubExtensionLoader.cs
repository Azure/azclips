// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Core.Abstractions;

namespace Tests.Common.Stubs;

public class StubExtensionLoader : IAssemblyLoadingProvider
{
    private readonly Dictionary<string, Assembly> _extensions;

    public StubExtensionLoader(Assembly extensionAssembly, string extensionName)
        : this(new Dictionary<string, Assembly> { [extensionName] = extensionAssembly })
    {
    }

    // Scenarios can span extensions, e.g. vm create needs its resource group created first.
    public StubExtensionLoader(IDictionary<string, Assembly> extensions)
    {
        _extensions = new Dictionary<string, Assembly>(extensions, StringComparer.OrdinalIgnoreCase);
    }

    public IList<InstalledExtensionRef>? GetAllInstalledExtensions() => null;

    public Assembly LoadExtension(string requestedExtensionName, string? version = null)
    {
        return _extensions.TryGetValue(requestedExtensionName, out var assembly)
            ? assembly
            : throw new InvalidOperationException($"Extension '{requestedExtensionName}' is not available in stub provider.");
    }
}
