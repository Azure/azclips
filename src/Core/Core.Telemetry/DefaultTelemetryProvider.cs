// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

public class DefaultTelemetryProvider : ITelemetryProvider
{
    public ActivitySource ActivitySource { get; }
    public TracerProvider TracerProvider { get; }

    public DefaultTelemetryProvider()
    {
        ActivitySource = new ActivitySource("Default");
        TracerProvider = TracerProvider.Default;
    }

    public Activity? StartActivity()
    {
        return null;
    }
}
