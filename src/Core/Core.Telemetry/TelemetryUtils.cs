// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Core.Telemetry;

public static class TelemetryUtils
{
    public static Activity GetActivityRoot(Activity activity)
    {
        while (activity.Parent is not null)
            activity = activity.Parent;
        return activity;
    }
}
