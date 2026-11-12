namespace Core.Agent.AgentMode;

/// <summary>The part of <see cref="ChatAgent"/> one turn of the chat loop uses.</summary>
internal interface ITurnAgent
{
    Task SendAsync(string prompt, CancellationToken ct);
    Task AbortAsync(CancellationToken ct);
    SessionErrored? LastError { get; }
}
