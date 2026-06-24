namespace Core.Agent;

// Placeholder entry — Core.Agent is exposed as a standalone exe so the
// project can be published independently, but the real chat loop still
// runs in-process inside TUI (see TUI/AgentMode/AgentModeEntry.cs).
// When we wire up a standalone agent REPL, replace this body.
internal static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("cirrus-agent: standalone entry not implemented yet.");
        Console.WriteLine("agent mode runs in-process via az fallback → Core.Tui --agent-mode.");
        return 0;
    }
}
