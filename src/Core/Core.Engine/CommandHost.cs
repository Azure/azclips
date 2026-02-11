using System.Collections.Concurrent;
using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Implementation;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

/// <summary>
/// PowerShell session host. Process-level singleton, created once at Import-Module time.
/// <para><b>Responsibilities:</b>
///   1. Manage per-module ALCs (cached — one ALC per extension assembly path)
///   2. Resolve command types from isolated ALCs
///   3. Create IProcessor instances and bind typed parameters
///   4. Delegate execution to <see cref="PSEngine"/>
/// </para>
/// <para><b>NOT responsible for:</b>
///   - Command line parsing (CLI concern)
///   - Output routing (PSEngine handles channel-based output pumping)
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
    private readonly PSEngine _engine;
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
        _engine = new PSEngine(serviceProvider);
        _loggingService = serviceProvider.GetService<ILoggingService>();
    }

    /// <summary>
    /// Invokes a command by assembly path and type name.
    /// The host loads the assembly in an isolated ALC, creates the IProcessor,
    /// binds typed parameters, and delegates execution to PSEngine.
    /// </summary>
    /// <param name="assemblyPath">Path to the extension assembly (e.g., Login.dll).</param>
    /// <param name="commandTypeName">Fully qualified command type name.</param>
    /// <param name="parameters">Typed parameters from PowerShell — set directly on command properties.</param>
    /// <param name="output">Per-invocation output delegate ($PSCmdlet.WriteObject).</param>
    /// <param name="token">Cancellation token for this invocation.</param>
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

        // 3. Create IProcessor instance
        IProcessor command = (IProcessor)(Activator.CreateInstance(commandType)
            ?? throw new InvalidOperationException($"Failed to create instance of '{commandTypeName}'."));

        // 4. Bind typed parameters directly (no string parsing — PS already has types)
        BindParameters(command, parameters);

        // 5. Delegate to PSEngine for context creation and execution
        _engine.Execute(command, output, token);
    }

    /// <summary>
    /// Invokes a command using extension name and command name.
    /// Resolves the command from the extension index (by-name lookup, similar to old PSEngine.GetCommand).
    /// Used when the ps1 script doesn't know the full type name.
    /// </summary>
    /// <param name="assemblyPath">Path to the extension assembly.</param>
    /// <param name="extensionName">Extension name for index lookup.</param>
    /// <param name="commandName">Command name (e.g., "Connect-CirrusAccount").</param>
    /// <param name="parameters">Typed parameters from PowerShell.</param>
    /// <param name="output">Per-invocation output delegate.</param>
    /// <param name="token">Cancellation token.</param>
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

        // 4. Create IProcessor and bind parameters
        IProcessor command = (IProcessor)(Activator.CreateInstance(commandMetadata.CommandType)
            ?? throw new InvalidOperationException($"Failed to create instance of '{commandMetadata.CommandType.FullName}'."));
        BindParameters(command, parameters);

        // 5. Execute
        _engine.Execute(command, output, token);
    }

    /// <summary>
    /// Binds typed parameters from PowerShell to the command's public properties by name.
    /// Uses case-insensitive matching since PS parameter names may differ in casing.
    /// </summary>
    private void BindParameters(IProcessor command, IDictionary<string, object> parameters)
    {
        var commandType = command.GetType();
        foreach (var (name, value) in parameters)
        {
            var prop = commandType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(command, value);
                _loggingService?.Verbose($"Bound parameter '{name}' on '{commandType.Name}'.");
            }
            else
            {
                _loggingService?.Verbose($"Skipping parameter '{name}' — no matching writable property on '{commandType.Name}'.");
            }
        }
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
