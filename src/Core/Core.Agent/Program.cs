// ┌─────────────────────────────────────────────────────────────────────────────┐
// │ Core.Agent Standalone Entry Point                                           │
// │                                                                             │
// │ This program provides a standalone way to test and run the AgentEngine      │
// │ independently from the main az CLI. Useful for:                             │
// │   • Testing agent behavior in isolation                                     │
// │   • Evaluating different agent modes (Cloud, Local, Copilot)                │
// │   • Development and debugging of agent features                             │
// │                                                                             │
// │ Usage:                                                                      │
// │   dotnet run --project src/Core/Core.Agent                                  │
// │   dotnet run --project src/Core/Core.Agent -- --local-model                 │
// │   dotnet run --project src/Core/Core.Agent -- --use-copilot                 │
// │   dotnet run --project src/Core/Core.Agent -- create a resource group       │
// └─────────────────────────────────────────────────────────────────────────────┘

using Core.Agent;
using Core.Implementation;

// Create telemetry service and agent engine
var telemetry = new ConsoleTelemetryService();
var agentEngine = new AgentEngine(telemetry);

// Execute the agent command
await agentEngine.ExecuteCommandAsync(args, CancellationToken.None);

// Get the extracted command from the agent
string extractedCommand = agentEngine.ExtractedCommand ?? string.Empty;

// Final output: either the command or empty string
if (!string.IsNullOrEmpty(extractedCommand))
{
    Console.WriteLine();
    Console.WriteLine("=== FINAL COMMAND ===");
    Console.WriteLine(extractedCommand);
}
else
{
    Console.WriteLine();
    Console.WriteLine("=== NO COMMAND TO EXECUTE ===");
}
