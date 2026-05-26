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
    /// Gets or sets the current sequential stage activity (e.g. the activity created for
    /// <c>ProcessorProcessAsyncStart</c>). The engine maintains this value across the
    /// <c>ProcessorProcessAsync*</c> lifecycle.
    /// </summary>
    Activity? CurrentStage { get; set; }

    /// <summary>
    /// Adds a property (tag) to an activity. By default, the property is added to the root
    /// activity. If <paramref name="activity"/> is provided (non-null), the property is added
    /// to that specific activity instead.
    /// </summary>
    /// <param name="key">The property (tag) name to add.</param>
    /// <param name="value">The property value. May be <c>null</c>.</param>
    /// <param name="activity">
    /// Optional activity to which the property should be added. When <c>null</c> (the default),
    /// the property is added to the root activity.
    /// </param>
    void AddProperty(string key, string? value, Activity? activity = null);

    /// <summary>
    /// Flushes all pending telemetry data.
    /// </summary>
    void Flush();
}
