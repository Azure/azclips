# CLI Telemetry Handoff Measurement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Measure the complete CLI telemetry handoff, including root-activity creation and replay of all pre-telemetry startup spans.

**Architecture:** Keep `PerformanceTimeline` unchanged and widen the existing startup measurement in `Program.Main`. Rename the typoed phase to `CLI.Performance.Telemetry`, wrapping `StartRootActivity(performance.StartedAt)` and `performance.Replay(_telemetryService)` in one measured region. Add focused test coverage for the phase duration while preserving existing replay and null-safety behavior.

**Tech Stack:** C#, .NET SDK from `global.json`, xUnit, `Activity`/`ITelemetryService`, `PerformanceTimeline`.

## Global Constraints

- Keep the change focused to `src/CLI/Program.cs` and `tests/Cli.Tests/Telemetry/PerformanceTimelineTests.cs`.
- Preserve null-safe behavior when telemetry is disabled.
- Do not modify unrelated existing worktree changes.
- Use the repository-pinned .NET SDK and run the targeted CLI tests.

---

### Task 1: Add coverage for the telemetry handoff phase

**Files:**
- Modify: `tests/Cli.Tests/Telemetry/PerformanceTimelineTests.cs`

**Interfaces:**
- Consumes: `PerformanceTimeline.Measure(string)`, `PerformanceTimeline.Replay(ITelemetryService)`, and the existing `RecordingTelemetryService`.
- Produces: A regression test proving that a phase spanning root activity creation and replay is emitted with a positive recorded duration.

- [ ] **Step 1: Add coverage for the combined handoff region**

Add this test to `PerformanceTimelineTests`. It deliberately keeps the handoff phase open while
creating the root activity and replaying the timeline, matching `Program.Main`:

```csharp
[Fact]
public void Replay_EmitsTelemetryHandoffPhaseWithRecordedDuration()
{
    using var telemetry = new RecordingTelemetryService();
    var timeline = new PerformanceTimeline();

    using (timeline.Measure("CLI.Performance.Telemetry"))
    {
        using var root = telemetry.StartRootActivity(timeline.StartedAt);
        Thread.Sleep(20);
        timeline.Replay(telemetry);
    }

    var handoff = Assert.Single(
        telemetry.Stopped,
        a => a.OperationName == "CLI.Performance.Telemetry");

    Assert.InRange(handoff.Duration.TotalMilliseconds, 15d, 5000d);
}
```

- [ ] **Step 2: Run the focused test to verify the timeline contract**

Run:

```powershell
dotnet test tests\Cli.Tests\Cli.Tests.csproj --filter FullyQualifiedName~PerformanceTimelineTests.Replay_EmitsTelemetryHandoffPhaseWithRecordedDuration
```

Expected: PASS. This test confirms there is no chicken-and-egg dependency: an open phase is emitted
using its current-time end boundary while replay runs, and its parent is the replayed
`CLI.Performance` span beneath the test root activity.

- [ ] **Step 3: Commit the regression test**

```powershell
git add tests\Cli.Tests\Telemetry\PerformanceTimelineTests.cs
git commit -m "test: cover CLI telemetry handoff timing"
```

### Task 2: Measure root activity creation and replay together

**Files:**
- Modify: `src/CLI/Program.cs:110-115`

**Interfaces:**
- Consumes: Existing `PerformanceTimeline`, `_telemetryService`, and `performance.StartedAt`.
- Produces: A `CLI.Performance.Telemetry` startup phase whose recorded interval covers both root-activity creation and `PerformanceTimeline.Replay`.

- [ ] **Step 1: Replace the narrow, typoed phase with the combined phase**

Change the existing block:

```csharp
using (performance.Measure("CLI.Performance.ASd"))
{
    activity = _telemetryService.StartRootActivity(performance.StartedAt);
}
performance.Replay(_telemetryService);
```

to:

```csharp
using (performance.Measure("CLI.Performance.Telemetry"))
{
    activity = _telemetryService.StartRootActivity(performance.StartedAt);
    performance.Replay(_telemetryService);
}
```

Do not alter exception handling, root-activity lifetime, or the existing startup phases.

- [ ] **Step 2: Run the focused timeline tests**

Run:

```powershell
dotnet test tests\Cli.Tests\Cli.Tests.csproj --filter FullyQualifiedName~PerformanceTimelineTests
```

Expected: PASS, including the new handoff-duration test and existing nesting, disabled-telemetry, out-of-order-disposal, enclosing-span, and leaked-handle tests.

- [ ] **Step 3: Run the CLI test project**

Run:

```powershell
dotnet test tests\Cli.Tests\Cli.Tests.csproj
```

Expected: PASS with no failures.

- [ ] **Step 4: Review the final diff and commit the implementation**

Run:

```powershell
git --no-pager diff --check
git --no-pager diff -- src/CLI/Program.cs tests/Cli.Tests/Telemetry/PerformanceTimelineTests.cs
```

Confirm the diff contains only the renamed/widened phase and its focused test, then commit:

```powershell
git add src\CLI\Program.cs tests\Cli.Tests\Telemetry\PerformanceTimelineTests.cs
git commit -m "feat: measure CLI telemetry handoff"
```
