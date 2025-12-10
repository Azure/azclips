using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

/// <summary>
/// DefaultCommandEngine handles structured command execution (e.g., "group create --name test").
///
/// EVOLUTION FROM ORIGINAL DESIGN:
/// Original (main branch): This WAS the only ICommandEngine - instantiated directly in Bootstrapper.
/// It used constructor-based dependency injection with ILogger, and instantiated
/// CommandParser and CommandBinder inline (not as injected dependencies).
///
/// Current (this branch): Now one of multiple engines coordinated by OrchestratorEngine.
/// Changes from original:
/// 1. Parser and Binder are now injected (better testability, explicit in Bootstrapper)
/// 2. Throws exceptions on failure (triggers orchestrator's fallback to AgentEngine)
/// 3. Added ExtractedCommand property (unused here, but required by ICommandEngine interface)
///
/// ROLE IN ORCHESTRATION:
/// This is the "fast path" - structured commands that match known syntax execute here.
/// On failure (unknown command, typo, natural language), exception triggers AgentEngine fallback.
/// </summary>
public class DefaultCommandEngine(CommandParser _parser, CommandBinder _binder, ILoggerService _logger) : ICommandEngine
{
    public string? ExtractedCommand { get; private set; }

    /// <summary>
    /// Executes a command by parsing, binding, and running it.
    /// </summary>
    public async Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default)
    {
        _logger.LogInfo($"Parsing command: {string.Join(" ", args)}");
        var parseResult = _parser.Parse(args);

        _logger.LogInfo($"Binding command: {string.Join(" ", parseResult.Commands)}");
        var command = _binder.Bind(parseResult);

        if (command == null)
        {
            _logger.LogWarning("No command found.");
            throw new InvalidOperationException("Command not found"); // Throw to trigger agent fallback
        }

        _logger.LogInfo($"Executing command: {command.GetType().Name}");
        await command.ExecuteAsync(await Bootstrapper.ServiceProviderAsync, parseResult.Options, cancellationToken);
        _logger.LogInfo("Command executed successfully.");
    }
}
