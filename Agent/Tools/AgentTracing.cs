using System.Diagnostics;

namespace Core.Agent;

/// <summary>Shared <see cref="ActivitySource"/> for everything in <c>Core.Agent</c>.</summary>
/// <remarks>
/// The name is "CLI", not an agent-specific name, and must stay that way. FileExporter
/// writes service.name from ActivitySource.Name and TraceUploader rebuilds parent/child
/// edges only within a service-name group, so an agent-specific name would detach every
/// span here from the CLI.Activity.AiRecovery parent on upload (Azure/CLIPS#602). Spans are
/// still distinguishable by their Agent.* / tool.* name prefixes. Matching by name is
/// enough — AddSource("CLI") listens to every ActivitySource with that name, not just the
/// provider's own instance.
/// </remarks>
public static class AgentTracing
{
    public const string SourceName = "CLI";

    public static readonly ActivitySource Source = new(SourceName);
}
