// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry.Trace;

namespace Core.Agent.Telemetry;

/// <summary>A borrowed DI view. Only the layer scope disposes the actual provider.</summary>
public sealed class AgentTelemetryProvider(ITelemetryProvider provider) : ITelemetryProvider
{
    public ActivitySource ActivitySource => provider.ActivitySource;
    public TracerProvider TracerProvider => provider.TracerProvider;
}

internal sealed class BorrowedAgentTelemetryService(ITelemetryService telemetry) : ITelemetryService
{
    public Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) =>
        telemetry.StartActivity(name, kind, startTime);

    public Activity? StartRootActivity(DateTimeOffset startTime = default, string? name = null) =>
        telemetry.StartRootActivity(startTime, name);

    public TelemetryActivityScope? StartActivityWithParent(
        string name, ActivityContext parentContext, ActivityKind kind = ActivityKind.Internal, DateTimeOffset startTime = default) =>
        telemetry.StartActivityWithParent(name, parentContext, kind, startTime);

    public TelemetryActivityScope? StartIndependentRootActivity(
        string name, ActivityKind kind = ActivityKind.Server, DateTimeOffset startTime = default, bool activate = true) =>
        telemetry.StartIndependentRootActivity(name, kind, startTime, activate);

    public Activity? TagInvocation(CommandInvocation commandInvocation) => telemetry.TagInvocation(commandInvocation);
    public Activity? AddTag(string propertyName, object? value, Activity? activity = null) =>
        telemetry.AddTag(propertyName, value, activity);

    public void Dispose() { }
}
