// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    /// <param name="startTime">
    /// Start timestamp for work that has already happened. Leave unset for live spans —
    /// per the OpenTelemetry spec this "SHOULD only be set when span creation time has
    /// already passed", e.g. replaying startup phases that ran before telemetry existed.
    /// </param>
    Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default);

    /// <param name="startTime">See <see cref="StartActivity"/>.</param>
    Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null);

    Activity? TagInvocation(CommandInvocation commandInvocation);
    Activity? AddTag(string propertyName, object? value, Activity? activity = null);
}
