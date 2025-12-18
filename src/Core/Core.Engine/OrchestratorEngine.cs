using Core.Abstractions;

namespace Core.Engine;

/// <summary>
/// OrchestratorEngine coordinates between structured command execution and AI-powered natural language processing.
///
/// WHY THIS EXISTS:
/// Original design (main branch) had only DefaultCommandEngine - it parsed structured commands
/// like "group create --name test" and threw exceptions for anything it couldn't understand.
///
/// This orchestrator was added to enable graceful fallback to AI for:
/// - Natural language queries: "create a resource group called test"
/// - Typos and mistakes: "groop create" → AI suggests "group create"
/// - Exploratory queries: "how do I list my resource groups?"
/// - Command generation: AI can generate executable commands from descriptions
///
/// EXECUTION FLOW:
/// 1. Try DefaultCommandEngine (fast path for structured commands)
/// 2. On failure → invoke AgentEngine (AI processes natural language)
/// 3. If AI returns executable command → retry with DefaultCommandEngine
/// 4. Repeat up to maxTries (allows AI to refine commands)
///
/// FLAVOR HANDLING:
/// - The AZURECLI_FLAVOR environment variable can be set to customize behavior
/// - "azure cli" (default): Full orchestration with structured command parsing followed by AI fallback
/// - "azure powershell": Bypass structured parsing and go directly to AI agent
/// - This allows integration with external systems that need different execution paths
///
/// NOTE: Flavor could be injected via constructor (better testability) but environment
/// variable keeps integration simple and flexible.
/// </summary>
public class OrchestratorEngine : ICommandEngine
{
    private readonly DefaultCommandEngine _defaultEngine;
    private readonly AgentEngine _agentEngine;
    private readonly CommandParser _parser;
    private readonly CommandBinder _binder;
    private readonly ILoggerService _logger;
    private readonly IOutputAdapter _output;

    public string? ExtractedCommand => _agentEngine.ExtractedCommand;

    public OrchestratorEngine(
        DefaultCommandEngine defaultEngine,
        AgentEngine agentEngine,
        CommandParser parser,
        CommandBinder binder,
        ILoggerService telemetry,
        IOutputAdapter output)
    {
        _defaultEngine = defaultEngine;
        _agentEngine = agentEngine;
        _parser = parser;
        _binder = binder;
        _logger = telemetry;
        _output = output;
    }

    public async Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken = default)
    {
        // Detect CLI flavor to determine execution path
        var flavor = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";

        if (flavor == "azure powershell")
        {
            // Direct-to-AI path: Skip structured parsing and use natural language processing only
            await _agentEngine.ExecuteCommandAsync(args, cancellationToken);
            return;
        }

        // Standard path: Full orchestration with structured command parsing → AI fallback
        const int maxTries = 5;
        int attempt = 0;
        string[] extractedArgs = args;

        while (attempt < maxTries)
        {
            attempt++;

            try
            {
                // Try the structured command engine first (fast path)
                await _defaultEngine.ExecuteCommandAsync(extractedArgs, cancellationToken);
                return; // Success - command executed
            }
            catch (NotImplementedException ex)
            {
                // NotImplementedException means command recognized but not yet implemented
                // This is not an error - just inform the user and exit successfully
                await _output.WriteWarningAsync($"\nNote: {ex.Message}");
                return;
            }
            catch (Exception)
            {
                // Any other exception: command not found, parse error, typo, natural language, etc.
                // Fallback to AI agent for interpretation
                await _agentEngine.ExecuteCommandAsync(extractedArgs, cancellationToken);

                // Check if agent extracted an executable command from the response
                var extractedCommand = _agentEngine.ExtractedCommand;

                if (string.IsNullOrWhiteSpace(extractedCommand))
                {
                    // Agent provided information/explanation but no executable command
                    // This is valid (e.g., answering "how do I...?" questions) - exit successfully
                    return;
                }

                // Agent generated a command - retry through DefaultEngine to execute it
                // Skip first token (typically "az") and parse remaining args
                extractedArgs = extractedCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
                await _output.WriteLineAsync($"\nRetrying with command: {extractedCommand}\n");
            }
        }

        // Exceeded retry limit - this shouldn't happen but protect against infinite loops
        throw new Exception($"Failed to execute command after {maxTries} attempts.");
    }
}

