// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>
/// Closed vocabulary for the <c>agent.modelSource</c> telemetry dimension. The dimension is
/// documented as exactly these five values, so the set is a type rather than a convention:
/// a mistyped literal cannot reach the exporter and quietly create a sixth value.
/// </summary>
public enum AgentModelSource
{
    /// <summary>No model could be established, so <c>agent.model</c> is absent.</summary>
    Unavailable,

    /// <summary>The model was read from the agent's own configuration before startup.</summary>
    Configuration,

    /// <summary>The runtime reported its selection, by session event or metadata query.</summary>
    Sdk,

    /// <summary>The model was observed on an assistant usage event within a send.</summary>
    Usage,

    /// <summary>The model was read from the gateway's <c>x-ms-served-model</c> response header.</summary>
    Response,
}

/// <summary>Wire mapping for <see cref="AgentModelSource"/>.</summary>
public static class AgentModelSourceExtensions
{
    /// <summary>
    /// Maps a source to the string the dimension carries. Exhaustive by construction: a member
    /// added without a mapping throws here instead of exporting an undocumented value.
    /// </summary>
    public static string Wire(this AgentModelSource source) => source switch
    {
        AgentModelSource.Unavailable => "unavailable",
        AgentModelSource.Configuration => "configuration",
        AgentModelSource.Sdk => "sdk",
        AgentModelSource.Usage => "usage",
        AgentModelSource.Response => "response",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unmapped agent model source."),
    };
}
