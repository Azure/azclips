using Core.Abstractions;

namespace Core.Cli;

/// <summary>
/// Orchestrator engine that tries the default engine first,
/// and falls back to the agent engine if that fails.
/// </summary>
public class OrchestratorEngine : ICommandEngine
{
    private readonly DefaultCommandEngine _defaultEngine;
    private readonly AgentEngine _agentEngine;
    private readonly CommandParser _parser;
    private readonly CommandBinder _binder;
    private readonly ITelemetryService _telemetry;
    private readonly IOutputAdapter _output;

    public string? ExtractedCommand => _agentEngine.ExtractedCommand;

    public OrchestratorEngine(
        DefaultCommandEngine defaultEngine,
        AgentEngine agentEngine,
        CommandParser parser,
        CommandBinder binder,
        ITelemetryService telemetry,
        IOutputAdapter output)
    {
        _defaultEngine = defaultEngine;
        _agentEngine = agentEngine;
        _parser = parser;
        _binder = binder;
        _telemetry = telemetry;
        _output = output;
    }

    public async Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try the default engine first
            await _defaultEngine.ExecuteCommandAsync(args, cancellationToken);
        }
        catch (NotImplementedException ex)
        {
            // NotImplementedException is not an error - it's a success with a note
            await _output.WriteWarningAsync($"\nNote: {ex.Message}");
            return; // Exit successfully
        }
        catch (Exception)
        {
            // Any other exception: hand to agent for natural language processing
            await _agentEngine.ExecuteCommandAsync(args, cancellationToken);

            // Check if agent extracted a command
            var extractedCommand = _agentEngine.ExtractedCommand;
            
            if (string.IsNullOrWhiteSpace(extractedCommand))
            {
                // Agent didn't provide an executable command - EXIT CONDITION (silent success)
                return;
            }

            // For PowerShell flavor, the marker is already output by AgentEngine
            // Don't try to execute PowerShell cmdlets from C# - PowerShell will handle it
            var flavor = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";
            if (flavor == "azure powershell")
            {
                // PowerShell will execute the command, we're done here
                return;
            }

            // Agent provided a command - try to execute it (Azure CLI mode only)
            try
            {
                // Parse the extracted command
                var extractedArgs = extractedCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var parseResult = _parser.Parse(extractedArgs);
                var command = _binder.Bind(parseResult);

                if (command == null)
                {
                    // Command not found after agent suggestion - EXIT CONDITION (silent success)
                    return;
                }

                // Execute the extracted command
                await command.ExecuteAsync(parseResult.Options, cancellationToken);
            }
            catch (NotImplementedException ex)
            {
                // NotImplementedException on retry - show note and EXIT
                await _output.WriteWarningAsync($"\nNote: {ex.Message}");
                return;
            }
            catch (Exception)
            {
                // Command execution failed after agent suggestion - EXIT CONDITION (silent success)
                // The agent already provided explanation, no need for additional error
                return;
            }
        }
    }
}
