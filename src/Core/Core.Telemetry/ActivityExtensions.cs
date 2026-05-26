// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Core.Telemetry;

public static class ActivityExtensions
{
    public static Activity GetRoot(this Activity activity)
    {
        while (activity.Parent is not null)
            activity = activity.Parent;
        return activity;
    }

    public static Activity? SetEventName(this Activity? activity, string eventName)
    {
        activity?.SetTag(
            CoreTelemetryProperties.EventNameTag,
            $"{CoreTelemetryProperties.ProductName}.{eventName}");
        return activity;
    }
}
