# CLI Telemetry Handoff Measurement — Design

**Date:** 2026-08-11
**Status:** Approved
**Scope:** `src/CLI/Program.cs`, `tests/Cli.Tests/Telemetry/PerformanceTimelineTests.cs`

## Problem

`Program.Main` currently measures only the call to `StartRootActivity` with the phase name
`CLI.Performance.ASd`. The subsequent `PerformanceTimeline.Replay` call is not included in that
phase, so the performance timeline does not show the complete handoff from recorded startup
timings to live telemetry.

## Design

Rename the phase to `CLI.Performance.Telemetry` and wrap both operations:

```csharp
using (performance.Measure("CLI.Performance.Telemetry"))
{
    activity = _telemetryService.StartRootActivity(performance.StartedAt);
    performance.Replay(_telemetryService);
}
```

The phase starts immediately before root-activity creation and ends after all recorded phases
have been replayed. The existing `PerformanceTimeline` replay behavior remains unchanged:
recorded container and service spans are emitted beneath `CLI.Performance`, and the new
telemetry-handoff phase is a sibling in that startup timeline.

## Error handling

No new exception handling is introduced. Existing startup exceptions continue through `Main`'s
existing catch/finally flow. If telemetry is disabled, `StartRootActivity` and replayed
`StartActivity` calls remain null-safe, while the measured phase still records its local timing.

## Testing

Update the timeline tests to verify that a measured telemetry-handoff phase preserves a positive
duration when it covers root-activity creation and replay. Existing tests continue to cover
replay nesting, disabled telemetry, out-of-order disposal, enclosing-span bounds, and leaked
handles.
