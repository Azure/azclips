using System.Reflection;
using System.Runtime.Loader;

namespace Core.Engine;

/// <summary>
/// Per-module AssemblyLoadContext for PowerShell extension isolation.
/// <para><b>Design:</b> Shared types (IProcessor, DI interfaces, delegates) resolve from
/// the default ALC to maintain type identity across modules. Module-specific dependencies
/// (Azure SDKs, serializers, etc.) are loaded in isolation to prevent version conflicts
/// when multiple Cirrus modules are loaded in the same PowerShell session.</para>
/// </summary>
public class ModuleAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _moduleDepsDir;
    private readonly AssemblyDependencyResolver _resolver;

    public ModuleAssemblyLoadContext(string mainAssemblyPath)
        : base(name: Path.GetFileNameWithoutExtension(mainAssemblyPath), isCollectible: false)
    {
        _moduleDepsDir = Path.GetDirectoryName(mainAssemblyPath)!;
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Let shared abstractions resolve from the default ALC.
        // This is critical for type identity: IProcessor from Login.dll must be
        // the same type as IProcessor from Group.dll.
        if (IsSharedAssembly(assemblyName))
        {
            return null; // fall through to default ALC
        }

        // Try the dependency resolver first (uses .deps.json)
        string? resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolvedPath != null)
        {
            return LoadFromAssemblyPath(resolvedPath);
        }

        // Try the module's directory as fallback
        string candidate = Path.Combine(_moduleDepsDir, $"{assemblyName.Name}.dll");
        if (File.Exists(candidate))
        {
            return LoadFromAssemblyPath(candidate);
        }

        // Fall through to default ALC
        return null;
    }

    /// <summary>
    /// Assemblies that MUST be shared across all modules to maintain type identity.
    /// </summary>
    private static bool IsSharedAssembly(AssemblyName assemblyName)
    {
        return assemblyName.Name switch
        {
            "Core.Abstractions" => true,
            "Core.Engine" => true,
            "Core.Implementation" => true,
            "Core.Runtime" => true,
            _ when assemblyName.Name?.StartsWith("Microsoft.Extensions.DependencyInjection") == true => true,
            _ => false
        };
    }
}
