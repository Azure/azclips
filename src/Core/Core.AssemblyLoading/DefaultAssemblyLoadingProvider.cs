// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.AssemblyLoading;

using System.Reflection;
using Core.Abstractions;

public class DefaultAssemblyLoadingProvider(): IAssemblyLoadingProvider
{
    private const string cirrusPrefix = "Microsoft.Azure.Cirrus.Extensions.";

    //Yabo: Default implementation of assembly loading.
    public Assembly LoadExtension(string extensionName, string? extensionVersion = null)
    {
        var assemblyName = cirrusPrefix + extensionName;
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

    public IList<InstalledExtensionRef>? GetAllInstalledExtensions()
    {
        // Default implementation does not support extension discovery; return null to indicate no extensions found.
        return null;
    }
}