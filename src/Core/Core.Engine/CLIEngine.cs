using System.Reflection;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Runtime.Handler;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

/// <summary>
/// CLI command engine — creates a scoped DI container per invocation, constructs
/// the IProcessor via ActivatorUtilities, binds parameters, and executes.
/// Extension loading and command resolution are handled by the caller.
/// </summary>
public class CLIEngine(IServiceProvider serviceProvider) : IEngine
{
    private bool _disposed = false;

    public void Execute(Type commandType, IDictionary<string, object> parameters, CommandOutputDelegate output, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var loggingService = serviceProvider.GetService<ILoggingService>();
        loggingService?.Info($"Executing command '{commandType.Name}'.");

        // Create a scoped DI container for this invocation.
        // Scoped services (ICommandAuthHandler, ICommandTelemetryHandler,
        // IRuntimeHttpPipeline, ICommandOutputHandler) are fresh per scope.
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        // Create per-invocation output handler wrapping the caller's delegate
        ICommandOutputHandler outputHandler = new CommandOutputHandler { WriteOutput = output };

        // Construct the processor with constructor injection;
        // outputHandler is passed as an extra parameter (not from DI)
        IProcessor command = (IProcessor)ActivatorUtilities.CreateInstance(scopedProvider, commandType, outputHandler);

        // Bind parameters (CLI args or PS params) onto public properties
        BindParameters(command, parameters, loggingService);

        // Execute
        ExecuteCommandAsync(command, cancellationToken).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Binds typed parameters to the processor's public properties by name.
    /// Uses case-insensitive matching.
    /// </summary>
    private static void BindParameters(IProcessor command, IDictionary<string, object> parameters, ILoggingService? logger)
    {
        var commandType = command.GetType();
        foreach (var (name, value) in parameters)
        {
            var prop = commandType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(command, value);
                logger?.Verbose($"Bound parameter '{name}' on '{commandType.Name}'.");
            }
            else
            {
                logger?.Verbose($"Skipping parameter '{name}' — no matching writable property on '{commandType.Name}'.");
            }
        }
    }

    private async Task ExecuteCommandAsync(IProcessor command, CancellationToken cancellationToken = default)
    {
        await Task.Run(async () =>
        {
            await command.ProcessAsync();
        }, cancellationToken);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
