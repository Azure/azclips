using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Core.Telemetry;

/// <summary>
/// Isolated AssemblyLoadContext for loading OpenTelemetry dependencies
/// without conflicting with PowerShell's built-in DiagnosticSource.
/// </summary>
internal class TelemetryLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public TelemetryLoadContext(string pluginPath) : base(isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Always load these dependencies in isolated context to avoid version conflicts
        if (assemblyName.Name == "OpenTelemetry" ||
            assemblyName.Name == "OpenTelemetry.Api" ||
            assemblyName.Name == "OpenTelemetry.Api.ProviderBuilderExtensions" ||
            assemblyName.Name?.StartsWith("System.Diagnostics.DiagnosticSource") == true)
        {
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
        }

        // For other assemblies, use default context (shared with PowerShell)
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
