// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.Tracing;
using BenchmarkDotNet.Engines;

namespace Benchmark.Telemetry;

/// <summary>
/// Tracks whether the BenchmarkDotNet engine is inside the measured (WorkloadActual) stage.
/// </summary>
/// <remarks>
/// Counting iterations instead would be wrong: BenchmarkDotNet's jitting stage invokes the workload
/// once or twice before warmup begins, so a counted boundary is offset, and it drifts silently
/// whenever the configured warmup or iteration counts change. <c>EngineEventSource</c> is the
/// engine's own signal, so it is exact and needs no knowledge of the counts.
/// </remarks>
internal sealed class BenchmarkStageListener : EventListener
{
    // EventListener can deliver OnEventSourceCreated from the base constructor, before this type's
    // field initializers run, and events arrive on the engine's thread. Both are fine for a
    // volatile bool defaulting to false.
    private volatile bool _isMeasured;

    internal bool IsMeasured => _isMeasured;

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        base.OnEventSourceCreated(eventSource);

        if (string.Equals(eventSource.Name, EngineEventSource.SourceName, StringComparison.Ordinal))
        {
            EnableEvents(eventSource, EventLevel.Informational);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        switch (eventData.EventId)
        {
            case EngineEventSource.WorkloadActualStartEventId:
                _isMeasured = true;
                break;

            case EngineEventSource.WorkloadActualStopEventId:
                _isMeasured = false;
                break;
        }
    }
}
