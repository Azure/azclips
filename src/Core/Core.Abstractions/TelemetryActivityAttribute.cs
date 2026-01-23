using System;
using System.Diagnostics;

namespace Core.Abstractions;

/// <summary>
/// Marks a method for telemetry activity interception.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class TelemetryActivityAttribute : Attribute
{
    /// <summary>
    /// Optional activity name. Defaults to "{TypeName}.{MethodName}" when not set.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Activity kind to use when starting the activity.
    /// </summary>
    public ActivityKind Kind { get; init; } = ActivityKind.Internal;
}
