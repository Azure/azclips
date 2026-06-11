using Core.Abstractions;

namespace Core.Agent.Hosting;

/// <summary>
/// Agent-mode replacement for ConsoleLoggingService. Drops Verbose / Debug /
/// Info / Log (noise that would race the spinner), promotes Warn / Error to
/// HostLogEmitted events so they go through the renderer's lock instead of
/// bypassing it. No log line is silently lost: anything above Info maps to
/// an event the renderer will draw.
/// </summary>
public sealed class RendererLoggingService : ILoggingService
{
    private readonly IAgentRenderer _renderer;

    public RendererLoggingService(IAgentRenderer renderer) => _renderer = renderer;

    public void Log(string message)     { }
    public void Info(string message)    { }
    public void Verbose(string message) { }
    public void Debug(string message)   { }

    public void Warn(string message) =>
        _renderer.OnEvent(new HostLogEmitted(HostLogLevel.Warn, message ?? string.Empty, DateTimeOffset.UtcNow));

    public void Error(string message) =>
        _renderer.OnEvent(new HostLogEmitted(HostLogLevel.Error, message ?? string.Empty, DateTimeOffset.UtcNow));
}
