// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Core.Telemetry;

internal interface ITelemetryActivityLifecycle
{
    IDisposable? TryBeginActivity();
    void OnActivityCreated(Activity? activity);
}
