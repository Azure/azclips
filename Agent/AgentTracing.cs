using System.Diagnostics;

namespace Core.Agent;

/// <summary>Shared <see cref="ActivitySource"/> for everything in <c>Core.Agent</c>.</summary>
public static class AgentTracing
{
    public const string SourceName = "Cirrus.Agent";

    public static readonly ActivitySource Source = new(SourceName);
}
