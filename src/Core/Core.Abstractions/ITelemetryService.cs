// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Core.Abstractions;

/// <summary>
/// Service for managing Activities.
/// </summary>
public interface ITelemetryService : IDisposable
{
    /// <summary>
    /// Starts a new activity with the given <paramref name="name"/> and <paramref name="kind"/>.
    /// Returns <c>null</c> if no listener (TracerProvider) is attached to the underlying source.
    /// </summary>
    Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal);
    Activity? StartRootActivity();
    Activity? AddTag(string tagName, object value, Activity? activity = null);
}
