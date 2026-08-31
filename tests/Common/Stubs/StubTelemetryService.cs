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

    public Activity? TagInvocation(CommandInvocation commandInvocation) => Activity.Current;

    public Activity? AddTag(string propertyName, object? value, Activity? activity = null)
    {
        return new Activity(propertyName);
    }

    public void Dispose() => ActivitySource.Dispose();
}
