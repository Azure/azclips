# CLI Startup Performance Telemetry — Design

**Date:** 2026-08-10
**Status:** Approved
**Scope:** `src/CLI/Program.cs`, `Core.Telemetry`, `Core.Abstractions`

## Problem

The CLI's trace begins at `TelemetryService.StartRootActivity()`, which runs *after* the DI
container is built and after `ILoggingService`, `IConfigurationService`, and `ITelemetryService`
are resolved. Everything before that point is invisible:

- registration callbacks and `BuildServiceProvider()`
- first-touch construction of the core singletons, including `CLITelemetryProvider`, which builds
  the entire OpenTelemetry `TracerProvider`, HTTP instrumentation, and file exporter

A `TracerProvider` build is not cheap, and today nobody can see it. The trace claims to describe
an `az` invocation while omitting its opening act.

## Constraint: you cannot trace the thing that builds your tracer

`CLITelemetryProvider`'s constructor creates both the `ActivitySource` and the `TracerProvider`
that listens to it. Until that constructor returns, no listener exists, so
`ActivitySource.StartActivity()` returns `null` and no span can be created in real time.

This was verified empirically, not assumed:

| Attempt | Result |
| --- | --- |
| `source.StartActivity(...)` before any listener | returns `null` — no span |
| `new Activity(...).Start()` before any listener, `Stop()` after | **is** exported, correct duration |
| Backdated span via `startTime:` + `SetEndTime()` | correct duration, nests via `Activity.Current`, one shared `TraceId` |

The hand-made `Activity` route works but is rejected — see [Rejected alternatives](#rejected-alternatives).

## Approach: record, then replay

Note `DateTime.UtcNow` as each startup phase begins and ends, then materialise those phases as
spans once telemetry exists, passing the recorded times via OpenTelemetry's `startTime` parameter.

This is the sanctioned use of that parameter. From the OpenTelemetry trace API specification:

> `Start timestamp`, default to current time. This argument SHOULD only be set when span creation
> time has already passed.

It is also the industry norm for processes that own their own `main()` without an externally
injected agent:

| Project | Mechanism |
| --- | --- |
| VS Code | `src/vs/base/common/performance.ts` records dependency-free marks; `timerService.ts` drains them into telemetry once the workbench is ready |
| Firebase `AppStartTrace` | captures `Process.getStartElapsedRealtime()`, materialises `_app_start` retroactively via `setClientStartTimeUs` |
| Chrome Navigation Timing | C++ records raw timestamps; JS materialises `PerformanceEntry` objects later |
| Git Trace2 | `trace2_initialize_clock()` is the first statement of `init_git()`; events report `t_abs` off that anchor |

The contrasting approach — bootstrap the tracer before application code — is only viable with an
external hook such as the CLR Profiling API (Datadog) or `DOTNET_STARTUP_HOOKS`. Those agents do
not measure their own initialisation, so the same blind spot reappears one layer down.

### One clock, not two

Phases are timed with `DateTime.UtcNow`, which is exactly what `Activity.Start()` calls
internally (verified: `StartTimeUtc` falls within a `UtcNow` bracket taken around the call).

A monotonic `Stopwatch` was considered and rejected. `Activity` is wall-clock throughout, so a
monotonic startup region would leave the startup span's end time and the following live span's
start time derived from different sources, free to disagree. Matching `Activity` keeps the
timeline internally consistent. The exposure — an NTP or VM clock step during the ~100 ms of CLI
startup — is negligible next to that.

`PerformanceTimeline` is therefore not a second timer. It is the same clock reading, taken by us
because `Activity` is not yet available to take it.

## Trace shape

```
CLI.Activity.Root                  ← backdated to the first line of Main
├── CLI.Performance                ← Main entry → core services resolved
│   ├── CLI.Performance.Container  ← registration callbacks + BuildServiceProvider()
│   └── CLI.Performance.Services   ← first-touch of logger / config / telemetry
└── AzureCLI.ProcessCommand        ← unchanged
```

Splitting `Container` from `Services` answers a specific question: is startup cost *wiring* or
*first-touch construction*? `BuildServiceProvider()` only builds the container's metadata graph;
singletons are constructed lazily on first `GetRequiredService`. Those are different problems with
different fixes.

The root is backdated to the first line of `Main`, not to OS process start. Process start would
include native host, CLR init, and JIT — real cost, but not actionable from managed code, and it
would report bogus multi-minute spans when `Program.Main` is invoked in-process by tests and
benchmarks.

Because the root is the earliest mark, no child can begin before its parent, so Jaeger and
Application Insights render the tree correctly.

### Paths that emit nothing

`--upload-telemetry`, `--print-probe`, `--completions`, and `--completion-candidates` all return
before `StartRootActivity` is called, so no trace is written. This is existing behaviour and is
preserved deliberately.

## Components

### `PerformanceTimeline` (nested in `Program`, `internal sealed`)

Records phases and replays them. Roughly 45 lines. `internal` rather than `private` so
`Cli.Tests` can reach it through the existing `InternalsVisibleTo` grant in `Core.Cli.csproj`.

Public surface:

- `DateTime StartedAt` — captured at construction, used to backdate the root
- `IDisposable Measure(string name)` — times a region
- `void Replay(ITelemetryService telemetry)` — materialises the recorded phases as spans

Nesting is supported. `Measure` pushes onto a stack of open phases and attaches each new phase to
whichever phase is currently open, so nested `using` blocks produce nested spans. `Replay` walks
the tree depth-first; children are created while their parent span is still current, which is the
same ambient `Activity.Current` mechanism ordinary `StartActivity` calls rely on. The enclosing
`CLI.Performance` span starts at `StartedAt` and ends at the end of the last top-level phase.

A flat list was considered and rejected: it silently flattens nested regions into siblings and
mis-reports the parent's end time, failing quietly the first time someone nests a `Measure`.

### Call sites in `Main`

```csharp
var performance = new PerformanceTimeline();

// ... --upload-telemetry and --print-probe early exits, unchanged ...

using (performance.Measure("CLI.Performance.Container"))
{
    AzclipsServiceProvider.PrependRegistration(...);
    AzclipsServiceProvider.OverrideRegistration(...);
    AzclipsServiceProvider.Initialize(_clientType);
}

// ... completions early exits, unchanged ...

using (performance.Measure("CLI.Performance.Services"))
{
    _logger              = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
    configurationService = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
    telemetryService     = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
}

using var activity = telemetryService.StartRootActivity(startTime: performance.StartedAt);
performance.Replay(telemetryService);
```

`Main` never handles a raw timestamp. Every `DateTime.UtcNow` call lives inside
`PerformanceTimeline`.

## Changes by file

| File | Change |
| --- | --- |
| `src/Core/Core.Abstractions/ITelemetryService.cs` | add `DateTimeOffset startTime = default` to `StartActivity` and `StartRootActivity` |
| `src/Core/Core.Telemetry/TelemetryService.cs` | forward `startTime` to `ActivitySource.StartActivity` |
| `src/Core/Core.Telemetry/DefaultTelemetryActivityNameFilter.cs` | allow exact `CLI.Performance` and prefix `CLI.Performance.` |
| `tests/Common/Stubs/StubTelemetryService.cs` | match the new signatures |
| `src/CLI/Program.cs` | `PerformanceTimeline`, two `Measure` blocks, backdated root, light restructuring |

Both parameters default to `default`, which `ActivitySource` already treats as "now", so every
existing call site is unchanged. No new files.

## Program.cs restructuring

Deliberately limited to the body of `Main`:

1. Hoist `using var activity` above the `try` so the nested `try` / `catch` collapses to one
   level, removing an indentation tier from the whole dispatch block.
2. Group the four early-exit handlers so the pre-DI exits (`--upload-telemetry`, `--print-probe`)
   and the post-DI exits (`--completions`, `--completion-candidates`) each read as one unit.

No extractions and no new files. Larger simplifications — moving the ~145-line agent-fallback
block, `BuildStreams`, `SpawnTelemetryUploader`, and the version/help dispatchers out of
`Program.cs` — are recorded as follow-ups below and are explicitly out of scope.

## Testing

`Cli.Tests`:

- a single `Measure` records a duration matching the elapsed wall time
- nested `Measure` blocks produce nested spans, not siblings
- `Replay` no-ops when nothing was measured
- `Replay` tolerates a telemetry service that returns `null` (telemetry disabled) without throwing

`Core.Telemetry.Tests`:

- `StartActivity` honours an explicit past `startTime`
- `StartRootActivity` honours an explicit past `startTime` and still applies its invariant tags
- `CLI.Performance` and `CLI.Performance.Container` pass `DefaultTelemetryActivityNameFilter.IsAllowed`

## Documentation

`wiki/02-deep-dive/telemetry.md` — update the activity-capture section and its mermaid diagram to
show the startup subtree and explain the record-then-replay mechanism.

## Rejected alternatives

**Bootstrap telemetry before the DI container.** Would make `StartActivity` work live during
startup, but requires hand-constructing `ConfigurationService` and `CLITelemetryProvider` outside
DI and registering the instances back in — a two-phase composition root. Worse, the
`TracerProvider` build must complete before it can measure anything, so the single most expensive
piece of startup becomes permanently invisible. This is the exact trade-off Datadog's CLR profiler
accepts, and it is the wrong one here.

**Hand-made `Activity` objects started in `Main`.** Verified to work in isolation, but rejected on
three counts. Phase spans complete before telemetry exists, so their `Stop()` finds no listener and
is dropped — recovering them means calling `SetEndTime()` now and deferring `Stop()` until after
DI, which is precisely the split-timing awkwardness the design set out to avoid. Hand-made
activities also belong to the unnamed default `ActivitySource`, so `CLITelemetryProvider` would
have to listen to `""` or `"*"`, admitting every `new Activity()` created by any dependency.
Finally `FileExporter` writes `service.name = activity.Source.Name` and `FileTelemetrySender`
groups traces by that value to set `cloud_RoleName`; startup spans would carry an empty service
name and split into their own group in Application Insights.

**Duration tags on the root span** (`performance.containerMs`, and so on). Fewest moving parts and
no backdating, but durations recorded as tags do not appear in a waterfall, which is the point of
the exercise.

**Monotonic `Stopwatch` timing.** See [One clock, not two](#one-clock-not-two).

## Follow-ups (out of scope)

Ordered by lines removed from `Program.cs`:

1. Agent fallback — `TryAgentFallbackAsync`, `FindAgentExe`, `JoinShellQuoted`, and the two env-var
   constants are ~145 lines of subprocess management sitting in the composition root.
2. `BuildStreams` — ~47 lines of stream plumbing, a natural `CliOutputStreams.Build(level)`.
3. `SpawnTelemetryUploader` — ~33 lines belonging beside `FileTelemetrySender`, which already owns
   the other half of the `--upload-telemetry` contract.
4. `DispatchVersion` / `DispatchHelp` / `EmitStructuredError` — cold-path rendering, ~50 lines.
5. The registration callbacks — a `CliServiceRegistration.Configure()` alongside the existing
   `CliExtensionServiceRegistration`.

Together these would reduce `Program.cs` from ~475 lines to roughly 120, leaving `Main` as a
readable sequence: measure, register, resolve, parse, dispatch, run.
