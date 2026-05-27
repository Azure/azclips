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
    /// Adds a numeric measure (tag) to the root activity of the current invocation. Use this
    /// for timings, counts, sizes, and other numeric business data.
    /// </summary>
    /// <param name="key">The measure name (without the <c>measures.</c> prefix).</param>
    /// <param name="value">The numeric value.</param>
    void AddMeasure(string key, double value);

    /// <summary>
    /// Flushes all pending telemetry data.
    /// </summary>
    void Flush();
}
