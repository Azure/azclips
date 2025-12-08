using Core.Abstractions;

namespace Core.Engine;

/// <summary>
/// 
/// Orchestrator engine that tries the default engine first,
/// and falls back to the agent engine if that fails.
/// Retries up to a maximum number of attempts.
/// 
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


        /// The execution flow is this one if involved from PowerShell
        /// 
        var flavor = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";
        if (flavor == "azure powershell")
        {
            await _agentEngine.ExecuteCommandAsync(args, cancellationToken);
            return;

        }

        else // Default to Azure CLI flavor
        {
            const int maxTries = 5;
            int attempt = 0;

            string[] extractedArgs = args;

            while (attempt < maxTries)
            {

                attempt++;

                try
                {
                    // Try the default engine first
                    await _defaultEngine.ExecuteCommandAsync(extractedArgs, cancellationToken);
                    return; // Success
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
                    await _agentEngine.ExecuteCommandAsync(extractedArgs, cancellationToken);

                    // Check if agent extracted a command
                    var extractedCommand = _agentEngine.ExtractedCommand;

                    if (string.IsNullOrWhiteSpace(extractedCommand))
                    {
                        // Agent didn't provide an executable command - EXIT CONDITION (silent success)
                        return;
                    }



                    // extract arguments for the next attempt
                    extractedArgs = extractedCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();

                    await _output.WriteLineAsync($"\nRetrying THE FOLLOWING command (command {extractedCommand})\n");
                }
            }

            // If we reach here, all attempts failed
            throw new Exception($"Failed to execute command after {maxTries} attempts.");
        }
    }
}
