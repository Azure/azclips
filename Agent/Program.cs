using Core.Agent.AgentMode;

namespace Core.Agent;

// Standalone agent process (azclips-agent.exe). The classic CLI spawns this with
// --agent-mode when an invocation fails; AgentModeEntry owns the chat loop.
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--agent-mode")
            return await AgentModeEntry.RunAsync(args);

        Console.WriteLine("azclips-agent: run via `--agent-mode` (spawned by the CLI on command failure).");
        return 0;
    }
}
