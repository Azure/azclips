// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

public class DefaultTelemetryProvider : ITelemetryProvider
{
    public ActivitySource ActivitySource { get; protected init; }
    public TracerProvider TracerProvider { get; protected init; }
    public virtual string? ShellType => null;
    public virtual IReadOnlyCollection<KeyValuePair<string, object?>> ActivityTags => [];

    public DefaultTelemetryProvider()
    {
        ActivitySource = new ActivitySource("Default");
        TracerProvider = TracerProvider.Default;
    }
}
