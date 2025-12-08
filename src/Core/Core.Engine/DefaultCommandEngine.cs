using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

/// <summary>
/// A default implementation of ICommandEngine that
/// parses command syntax, dynamically loads a module,
/// and delegates to the command implementation type to execute commands.
/// </summary>
/// <param name="Telemetry"></param>
public class DefaultCommandEngine(ITelemetryService Telemetry, IServiceScopeFactory serviceScopeFactory) : ICommandEngine
{
    private readonly CommandParser _parser = new();
    private readonly CommandBinder _binder = new();

    public async Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken)
    {
        // Placeholder implementation
        // todo: consider abstracting these steps as a pipeline
        Telemetry.Log($"Executing command: {string.Join(' ', args)}");
        var arguments = _parser.Parse(args);
        Telemetry.Log($"Parsed {arguments.Commands.Count()} commands and {arguments.Options.Count} options.");
        var command = _binder.Bind(arguments);
        if (command != null)
        {
            Telemetry.Log($"Command bound successfully. Executing command {command.GetType()} ...");
            using var scope = serviceScopeFactory.CreateScope(); // is scope necessary or is root provider sufficient?
            await command.ExecuteAsync(scope.ServiceProvider, cancellationToken);
        }
        else
        {
            Telemetry.Log("No command bound. Exiting.");
            throw new InvalidOperationException("No command bound.");
        }
    }
}
