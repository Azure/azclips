namespace Core.Agent.Hosting;

/// <summary>Renders streaming LLM text + structured agent events for a host (console / TUI / web).</summary>
public interface IAgentRenderer
{
    void OnText(string chunk);
    void OnEvent(AgentEvent ev);
    void OnSpinnerFrame(string frame, string verb);
    void OnSpinnerStop();
}
