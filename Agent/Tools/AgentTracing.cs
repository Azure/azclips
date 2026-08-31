using System.Diagnostics;

namespace Core.Agent;

/// <summary>Shared <see cref="ActivitySource"/> for everything in <c>Core.Agent</c>.</summary>
/// <remarks>
/// The name must stay "CLI", matching the CLI process. FileExporter writes service.name
/// from ActivitySource.Name, and TraceUploader rebuilds parent/child edges only within a
/// service-name group, so a different name here detaches every span from its parent on
/// upload. Spans stay identifiable by their Agent.* and tool.* prefixes.
/// </remarks>
public static class AgentTracing
{
    public const string SourceName = "CLI";

    public static readonly ActivitySource Source = new(SourceName);
}
