using Core.Abstractions;
using Core.Cli;

// Determine flavor from environment variable (set by PowerShell cmdlets)
// Default to "azure powershell" since this is the azv2 tool
var flavor = Environment.GetEnvironmentVariable("AZURECLI_FLAVOR") ?? "azure powershell";

// Setup output adapter
var output = new ConsoleOutputAdapter();

// Setup telemetry
var telemetry = new ConsoleTelemetryService();

// Setup parser and binder
var parser = new CommandParser();
var binder = new CommandBinder(telemetry);

// Setup engines with the appropriate flavor
var defaultEngine = new DefaultCommandEngine(parser, binder, telemetry);
var agentEngine = new AgentEngine(output, flavor);
var compositeEngine = new CompositeCommandEngine(defaultEngine, agentEngine, parser, binder, telemetry, output);

// Execute command
try
{
    // If no arguments provided, show help
    if (args.Length == 0)
    {
        Console.WriteLine("Azure CLI v2");
        Console.WriteLine("\nUsage:");
        Console.WriteLine("  azv2 <command> [options]           Execute a command");
        Console.WriteLine("  azv2 <natural language>            Ask the AI assistant");
        Console.WriteLine("\nExamples:");
        Console.WriteLine("  azv2 group list");
        Console.WriteLine("  azv2 group create --name mygroup --location eastus");
        Console.WriteLine("  azv2 how do I create a resource group?");
        Console.WriteLine("  azv2 create a new resource group for me");
        return 0;
    }
    
    await compositeEngine.ExecuteCommandAsync(args);
    
    // Note: PowerShell flavor command extraction is handled by CompositeEngine
    // but we don't output the __PSEXEC__ marker because PowerShell can't capture
    // stdout without buffering all output (which breaks real-time streaming)
    // User will see the generated command and can copy/paste it
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Fatal error: {ex.Message}");
    Console.ResetColor();
    return 1;
}

return 0;
