namespace Core.Agent.Hosting;

/// <summary>Renders streaming LLM text + structured agent events for a host (console / TUI / web).</summary>
public interface IAgentRenderer
{
    void OnText(string chunk);
    void OnEvent(AgentEvent ev);
    void OnSpinnerFrame(string frame, string verb);
    void OnSpinnerStop();

    // Hosts that own a single stdout/stdin (console) must serialize external
    // writers (approval prompt's WriteLine + ReadLine) against the internal
    // On* writers, otherwise SDK-thread OnEvent and the pump thread's prompt
    // interleave on the same line. Default is no-op so non-console hosts
    // (in-process TUI, web) keep working without ceremony.
    void RunExclusive(Action action) => action();
}
