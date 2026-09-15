// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.Tracing;
using BenchmarkDotNet.Engines;

namespace Benchmark.Telemetry;

/// <summary>
/// Tracks whether the BenchmarkDotNet engine is inside the measured (WorkloadActual) stage, from
/// the engine's own <c>EngineEventSource</c> signal.
/// </summary>
internal sealed class BenchmarkStageListener : EventListener
{
    // Written on the engine's thread, read on the benchmark's, and live before field initializers
    // run — the base constructor can raise OnEventSourceCreated.
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
