# Task 7 Report

## Implemented
- Added `ReplayEntries_LinksChildToParent_WhenTheyCameFromDifferentFiles` to `tests/Core.Telemetry.Tests/TraceReplayerTests.cs`.
- The test asserts both trace-id preservation and that the child activity's `ParentSpanId` equals the replayed parent's emitted `SpanId`.
- Comment explains the silent-failure mode: a missing remap falls through to `ActivitySpanId.CreateRandom()`, detaching the agent subtree.

## Existing helpers
- The file already had suitable helpers:
  - `MakeEntry(...)` for building `TraceEntry` instances.
  - `ReplayHarness` for capturing replayed activities.
- I reused those instead of adding duplicate `NewEntry`/`Replay` helpers.
- `MakeEntry` already initializes `Tags`, `Events`, and `Resource`, so no JsonElement issues were introduced.

## Validation
- Command: `dotnet test tests\\Core.Telemetry.Tests\\Core.Telemetry.Tests.csproj --filter "FullyQualifiedName~ReplayEntries_LinksChildToParent"`
- Output: **PASS** (`Passed: 1, Failed: 0, Skipped: 0`)
- This task expected PASS immediately; I did not force a red phase.

## Files changed
- `tests/Core.Telemetry.Tests/TraceReplayerTests.cs`
- `.superpowers/sdd/2026-08-31-ai-recovery-trace-separation/task-7-report.md`

## Self-review
- Test covers both required assertions.
- Reused existing helpers.
- No production code changed.
- Validation output was clean.

## Issues or concerns
- None.
