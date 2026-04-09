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

    Activity StartActivity(string name, ActivityKind kind = ActivityKind.Internal);

    /// <summary>
    /// Tags the current activity with module info from a resolved invocation.
    /// </summary>
    void ProcessInvocation(ICommandInvocation invocation);

    /// <summary>
    /// Flushes all pending telemetry data.
    /// </summary>
    void Flush();
}
