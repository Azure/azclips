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
/// FLAVOR HANDLING (addressing corrections.md concerns):
/// - PowerShell cmdlets set AZURECLI_FLAVOR="azure powershell" before calling CLI
/// - When PowerShell flavor detected, we bypass DefaultEngine and go straight to AgentEngine
/// - Why? PowerShell invokes CLI for natural language only (Invoke-AzV2 cmdlet)
/// - Structured commands in PowerShell use cmdlets directly (New-AzV2ResourceGroup), not CLI
/// - CLI invocations default to "azure cli" flavor and use full orchestration
///
/// NOTE: Flavor could be injected via constructor (better testability) but environment
/// variable keeps it simple and matches PowerShell's existing integration pattern.
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
        // Detect CLI flavor: "azure cli" (default, direct CLI invocation) or "azure powershell" (called from PowerShell)
        var flavor = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure cli";

        if (flavor == "azure powershell")
        {
            // PowerShell flavor: Only natural language processing needed
            // PowerShell uses cmdlets for structured commands, only invokes CLI for natural language (Invoke-AzV2)
            await _agentEngine.ExecuteCommandAsync(args, cancellationToken);
            return;
        }

        // Azure CLI flavor: Full orchestration with DefaultEngine → AgentEngine fallback
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
                // Skip first token (typically "azv2") and parse remaining args
                extractedArgs = extractedCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
                await _output.WriteLineAsync($"\nRetrying with command: {extractedCommand}\n");
            }
        }

        // Exceeded retry limit - this shouldn't happen but protect against infinite loops
        throw new Exception($"Failed to execute command after {maxTries} attempts.");
    }
}

