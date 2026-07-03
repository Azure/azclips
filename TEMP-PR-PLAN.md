# TEMP PR Plan - Cirrus Sweep (New Scope)

## Scope

Targeted sweep for remaining `Cirrus` references in:

- `tests/Core.Telemetry.Tests`
- `Agent`
- `Agent/tests`
- `tests/Benchmark`

## Directory Checklist

- [x] `tests/Core.Telemetry.Tests`
- [x] `Agent`
- [x] `Agent/tests`
- [x] `tests/Benchmark`

## Check Types

- [x] Reference/content check (`cirrus`, `Invoke-Cirrus*`, `Microsoft.Azure.Cirrus*`, `CIRRUS_*`)
- [x] Filename/folder-name check (any path segment containing `cirrus`)

## Notes

- Run checks first, no code changes until results are reviewed.
- Exclude generated outputs: `obj`, `bin`, `artifacts`, `devops/out`, `.git`.
- Check run (2026-07-03): reference hits = 13 across 5 files.
- Top reference files:
  - `Agent/tests/Core.Agent.Tests/ArmDetailsClientLocalTests.cs` (8)
  - `tests/Core.Telemetry.Tests/TelemetryPropertyFilterProcessorTests.cs` (2)
  - `Agent/ChatAgent.cs` (1)
  - `tests/Core.Telemetry.Tests/ActivityNameFilterProcessorTests.cs` (1)
  - `tests/Core.Telemetry.Tests/FileTelemetrySenderTests.cs` (1)
- Check run (2026-07-03): filename/folder-name hits = 0.
- Sweep update (2026-07-03): references in scope reduced from 13 to 0.
- Post-sweep verification (2026-07-03): reference hits = 0, filename/folder-name hits = 0.
