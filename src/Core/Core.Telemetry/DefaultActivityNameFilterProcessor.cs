// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;

namespace Core.Telemetry;

public sealed class DefaultActivityNameFilterProcessor : BaseProcessor<Activity>
{
    private readonly ITelemetryActivityNameFilter _filter;
    private long _droppedCount;

    public DefaultActivityNameFilterProcessor(ITelemetryActivityNameFilter filter)
    {
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    /// <summary>
    /// Total number of activities dropped across the process lifetime. Exposed
    /// for tests and diagnostics; not surfaced anywhere user-visible because
    /// logging dropped activity names could itself leak the existence of
    /// sensitive operations.
    /// </summary>
    internal long DroppedCount => Interlocked.Read(ref _droppedCount);

    public override void OnEnd(Activity data)
    {
        if (data is null) return;
        if (_filter.IsAllowed(data.OperationName)) return;

        // Clearing the Recorded flag signals downstream export processors to
        // skip this activity. See BatchActivityExportProcessor.OnEnd /
        // SimpleActivityExportProcessor.OnEnd in the OTel SDK source.
        data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        Interlocked.Increment(ref _droppedCount);
    }
}
