using System.Collections.Concurrent;
using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

/// <summary>
/// PowerShell session host. Process-level singleton, created once at Import-Module time.
/// <para><b>Responsibilities:</b>
///   1. Manage per-module ALCs (cached — one ALC per extension assembly path)
///   2. Resolve command types from isolated ALCs
///   3. Delegate execution to <see cref="PSEngine"/> (which handles scoped DI,
///      processor construction, parameter binding, and output routing)
/// </para>
/// <para><b>Lifecycle:</b>
///   - Created once per PS session via $global:CirrusCommandHost
///   - Shared across all Cirrus modules (Login, Group, Agent, etc.)
///   - Disposed when PS session ends or modules are removed
/// </para>
/// </summary>
public class CommandHost : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEngine _engine;
    private readonly ILoggingService? _loggingService;
    private bool _disposed;

    /// <summary>
    /// Per-module ALC cache. Key is the assembly file path.
    /// Thread-safe for concurrent invocations in the same PS session.
    /// </summary>
    private readonly ConcurrentDictionary<string, ModuleAssemblyLoadContext> _alcCache = new();

    /// <summary>
    /// Per-extension index cache. Key is the assembly file path.
    /// Extension indices are immutable after construction — safe to cache.
    /// </summary>
    private readonly ConcurrentDictionary<string, IExtensionIndex> _indexCache = new();

    public CommandHost(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _engine = serviceProvider.GetRequiredService<IEngine>();
        _loggingService = serviceProvider.GetService<ILoggingService>();
    }

    /// <summary>
    /// Invokes a command by assembly path and type name.
    /// The host loads the assembly in an isolated ALC, resolves the type,
    /// and delegates to PSEngine for scoped DI construction and execution.
    /// </summary>
    public void Invoke(
        string assemblyPath,
        string commandTypeName,
        IDictionary<string, object> parameters,
        CommandOutputDelegate output,
        CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _loggingService?.Info($"Invoking command '{commandTypeName}' from '{Path.GetFileName(assemblyPath)}'.");

        // 1. Load assembly in isolated ALC (cached per module)
        string fullPath = Path.GetFullPath(assemblyPath);
        var alc = _alcCache.GetOrAdd(fullPath, path => new ModuleAssemblyLoadContext(path));
        Assembly assembly = alc.LoadFromAssemblyPath(fullPath);

        // 2. Resolve command type from the module's ALC
        Type commandType = assembly.GetType(commandTypeName)
            ?? throw new InvalidOperationException($"Command type '{commandTypeName}' not found in '{assemblyPath}'.");

        // 3. Delegate to PSEngine — it handles scoped DI, ActivatorUtilities, parameter binding, and execution
        _engine.Execute(commandType, parameters, output, token);
    }

    /// <summary>
    /// Invokes a command using extension name and command name.
    /// Resolves the command from the extension index (by-name lookup).
    /// </summary>
    public void InvokeByName(
        string assemblyPath,
        string extensionName,
        string commandName,
        IDictionary<string, object> parameters,
        CommandOutputDelegate output,
        CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _loggingService?.Info($"Invoking command '{commandName}' from extension '{extensionName}'.");

        // 1. Load assembly in isolated ALC
        string fullPath = Path.GetFullPath(assemblyPath);
        var alc = _alcCache.GetOrAdd(fullPath, path => new ModuleAssemblyLoadContext(path));
        Assembly assembly = alc.LoadFromAssemblyPath(fullPath);

        // 2. Get or create ExtensionIndex
        IExtensionIndex extensionIndex = _indexCache.GetOrAdd(fullPath, _ =>
        {
            var typeName = $"{assembly.GetName().Name}.ExtensionIndex";
            Type indexType = assembly.GetType(typeName)
                ?? throw new InvalidOperationException($"Extension '{extensionName}' missing {typeName}.");
            return (IExtensionIndex)(Activator.CreateInstance(indexType)
                ?? throw new InvalidOperationException($"Failed to create ExtensionIndex for '{extensionName}'."));
        });

        // 3. Look up command metadata
        ICommandMetadata commandMetadata = extensionIndex.GetCommandMetaData(commandName, ClientType.PowerShell);
        _loggingService?.Verbose($"Resolved command '{commandName}' to type '{commandMetadata.CommandType.FullName}'.");

        // 4. Delegate to PSEngine — it handles scoped DI, ActivatorUtilities, parameter binding, and execution
        _engine.Execute(commandMetadata.CommandType, parameters, output, token);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _engine.Dispose();
                _alcCache.Clear();
                _indexCache.Clear();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
