// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;

namespace Tests.Common.Stubs;

public class StubTelemetryService : ITelemetryService
{
    public ActivitySource ActivitySource { get; } = new(Guid.NewGuid().ToString());

    public Activity? CurrentStage { get; set; }

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        => ActivitySource.StartActivity(name, kind);

    public void AddProperty(string key, string? value, Activity? activity = null)
    {
        if (string.IsNullOrEmpty(key)) return;

        var act = Activity.Current;
        if (act is null) return;

        var target = activity ?? act.GetRoot();
        target?.SetTag($"properties.{key}", value);
    }

    public void AddMeasure(string key, double value)
    {
        if (string.IsNullOrEmpty(key)) return;

        var activity = Activity.Current;
        if (activity is null) return;
        var target = activity.GetRoot();

        target?.SetTag($"measures.{key}", value);
    }


    public void Flush() { }

    public void Dispose() => ActivitySource.Dispose();
}
