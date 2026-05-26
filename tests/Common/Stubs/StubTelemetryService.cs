// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;

namespace Tests.Common.Stubs;

public class StubTelemetryService : ITelemetryService
{
    public ActivitySource ActivitySource { get; } = new(Guid.NewGuid().ToString());

    public Activity? CurrentStage { get; set; }

    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        => ActivitySource.StartActivity(name, kind);

    public void AddProperty(string key, string? value, Activity? activity = null) { }

    public void Flush() { }

    public void Dispose() => ActivitySource.Dispose();
}
