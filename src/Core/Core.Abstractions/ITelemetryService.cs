using System;
using System.Diagnostics;

namespace Core.Abstractions;

/// <summary>
/// Service for managing OpenTelemetry tracing and telemetry.
/// </summary>
public interface ITelemetryService : IDisposable
{
    /// <summary>
    /// Gets the ActivitySource for creating activities.
    /// </summary>
    ActivitySource ActivitySource { get; }

    Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal);

    /// <summary>
    /// Adds a string property to the root span for the current command invocation.
    /// </summary>
    /// <remarks>
    /// <para>Squad-facing enrichment API. The tag is written to the <b>root</b> activity
    /// (the top of the parent chain from <see cref="Activity.Current"/>) and is
    /// automatically prefixed with <c>properties.</c> so Kusto/Azure Monitor
    /// dashboards can filter cleanly (e.g. <c>properties.resourceType</c>).</para>
    /// <para>No-op when <see cref="Activity.Current"/> is <c>null</c> (e.g. in unit tests
    /// without an ambient activity).</para>
    /// </remarks>
    /// <param name="key">Property key (the <c>properties.</c> prefix is added automatically).</param>
    /// <param name="value">Property value. <c>null</c> is permitted.</param>
    void AddProperty(string key, string? value);

    /// <summary>
    /// Flushes all pending telemetry data.
    /// </summary>
    void Flush();
}
