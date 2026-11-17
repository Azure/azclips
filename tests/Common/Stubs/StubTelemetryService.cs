// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;

namespace Tests.Common.Stubs;

public class StubTelemetryService : ITelemetryService
{
    public ActivitySource ActivitySource { get; } = new("Stub");

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) =>
        ActivitySource.StartActivity(name, kind, parentContext: default, tags: null, links: null, startTime: startTime);

    public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null) =>
        ActivitySource.StartActivity(name ?? "Stub.Activity.Root", ActivityKind.Server, parentContext: default, tags: null, links: null, startTime: startTime);

    public TelemetryActivityScope? StartActivityWithParent(string name, ActivityContext parentContext,
        ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) =>
        parentContext == default
            ? CreateIndependentScope(ActivitySource, name, kind, startTime, activate: true)
            : (ActivitySource.StartActivity(name, kind, parentContext, startTime: startTime) is { } started ? new TelemetryActivityScope(started) : null);

    public TelemetryActivityScope? StartIndependentRootActivity(string name, ActivityKind kind = ActivityKind.Server,
        DateTimeOffset startTime = default, bool activate = true) =>
        CreateIndependentScope(ActivitySource, name, kind, startTime, activate);

    public static TelemetryActivityScope? CreateIndependentScope(ActivitySource source, string name, ActivityKind kind,
        DateTimeOffset startTime, bool activate)
    {
        var previous = Activity.Current;
        try
        {
            Activity.Current = null;
            var activity = source.CreateActivity(name, kind, parentId: string.Empty, idFormat: ActivityIdFormat.W3C);
            if (startTime != default)
                activity?.SetStartTime(startTime.UtcDateTime);
            activity?.Start();
            return activity is null ? null : new TelemetryActivityScope(activity, previous, restoreAmbient: true, activated: activate);
        }
        finally
        {
            if (!activate || Activity.Current is null)
                Activity.Current = previous;
        }
    }

    public Activity? TagInvocation(CommandInvocation commandInvocation) => Activity.Current;

    public Activity? AddTag(string propertyName, object? value, Activity? activity = null)
    {
        return new Activity(propertyName);
    }

    public void Dispose() => ActivitySource.Dispose();
}
