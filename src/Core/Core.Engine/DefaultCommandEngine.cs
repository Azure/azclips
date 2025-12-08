using Core.Abstractions;

namespace Core.Engine;

/// <summary>
/// Default command engine that parses, binds, and executes commands.
/// Does not throw exceptions - returns null on failure for agent fallback.
/// </summary>
public class DefaultCommandEngine : ICommandEngine
{
    private readonly CommandParser _parser;
    private readonly CommandBinder _binder;
    private readonly ITelemetryService _telemetry;

    public string? ExtractedCommand { get; private set; }

    public DefaultCommandEngine(CommandParser parser, CommandBinder binder, ITelemetryService telemetry)
    {
        _parser = parser;
        _binder = binder;
        _telemetry = telemetry;
    }

    /// <summary>
    /// Executes a command by parsing, binding, and running it.
    /// </summary>
    public async Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default)
    {
        _telemetry.LogInfo($"Parsing command: {string.Join(" ", args)}");
        var parseResult = _parser.Parse(args);

        _telemetry.LogInfo($"Binding command: {string.Join(" ", parseResult.Commands)}");
        var command = _binder.Bind(parseResult);

        if (command == null)
        {
            _telemetry.LogWarning("No command found.");
            throw new InvalidOperationException("Command not found"); // Throw to trigger agent fallback
        }

        _telemetry.LogInfo($"Executing command: {command.GetType().Name}");
        await command.ExecuteAsync(parseResult.Options, cancellationToken);
        _telemetry.LogInfo("Command executed successfully.");
    }
}
