// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Core.Abstractions;

public static class TelemetryServiceExtensions
{
    public static void Emit(this ITelemetryService telemetryService, string eventName, ActivityKind kind = ActivityKind.Internal, Dictionary<string, string>? properties = null)
    {
        var act = telemetryService.StartActivity(eventName, kind);

        if (properties is not null)
        {
            foreach (var kvp in properties)
            {
                act?.AddTag(kvp.Key, kvp.Value);
            }
        }

        telemetryService.CurrentStage = null;
        act?.Dispose();
    }
}
