// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;

namespace Core.Telemetry;

/// <summary>Includes vetted nested instrumentation at start, not during export or replay.</summary>
public sealed class SourceHierarchyProcessor(ITelemetryActivityNameFilter nameFilter) : BaseProcessor<Activity>
{
    public override void OnStart(Activity data)
    {
        if (nameFilter.IsAllowed(data.OperationName))
            TelemetryHierarchy.Record(data);
    }
}
