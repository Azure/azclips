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
| `new Activity(...).Start()`, exported via `AddSource("")` | throws — the default source cannot be registered |
| `new Activity(...).Start()`, exported via `AddSource("*")` | exports nothing — `Recorded` is never set |
| Named `ActivitySource` + listener at line 1, `Stop()` deferred | works — correct duration, source, and tree |
| Backdated span via `startTime:` + `SetEndTime()` | works — correct duration, nests via `Activity.Current`, one shared `TraceId` |

Live spans at line 1 are therefore possible, but only by installing process-wide tracing state
before the program's first statement — see [Rejected alternatives](#rejected-alternatives).

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

### `PerformanceTimeline` (`src/CLI/Telemetry/PerformanceTimeline.cs`)

Records phases and replays them. Roughly 45 lines, `internal sealed`, namespace
`Core.Cli.Telemetry`, alongside `CLITelemetryProvider`. `Cli.Tests` reaches it through the existing
`InternalsVisibleTo` grant in `Core.Cli.csproj`. It depends only on `ITelemetryService`, so it can
move to `Core.Telemetry` later if TUI or PowerShell want the same measurement.

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
| `src/CLI/Telemetry/PerformanceTimeline.cs` | **new** — records and replays startup phases |
| `src/CLI/Program.cs` | two `Measure` blocks, backdated root, light restructuring |

Both parameters default to `default`, which `ActivitySource` already treats as "now", so every
existing call site is unchanged.

## Program.cs restructuring

Deliberately limited to the body of `Main`:

1. Hoist `using var activity` above the `try` so the nested `try` / `catch` collapses to one
   level, removing an indentation tier from the whole dispatch block.
2. Group the four early-exit handlers so the pre-DI exits (`--upload-telemetry`, `--print-probe`)
   and the post-DI exits (`--completions`, `--completion-candidates`) each read as one unit.

No extractions from `Program.cs` beyond the two items above. Larger simplifications — moving the
~145-line agent-fallback block, `BuildStreams`, `SpawnTelemetryUploader`, and the version/help
dispatchers out of `Program.cs` — are recorded as follow-ups below and are explicitly out of scope.

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

**Spans started live at line 1 of `Main`.** Two shapes were tested against the real OpenTelemetry
SDK, not reasoned about.

*Bare `new Activity("...").Start()`* cannot work. Such an activity belongs to the unnamed default
`ActivitySource`, and `AddSource("")` throws `ArgumentException: Must not be null or whitespace`;
`AddSource("*")` builds but exports nothing, because OpenTelemetry only exports activities whose
`Recorded` flag is set, and only a listener present at `Start()` can set it. The activity can be
handed directly to a `BaseExporter<Activity>` (verified: duration preserved), but that bypasses
`DefaultActivityNameFilterProcessor` and `DefaultTelemetryPropertyFilterProcessor` — the privacy
allow-lists — so it would have to be re-plumbed by hand.

*A named `ActivitySource("CLI")` plus an `ActivityListener` at line 1* does work, fully: verified
correct durations, `source.Name = "CLI"`, correct parent/child tree, and a root span genuinely
starting at `Main` entry with no backdating. It is rejected on cost, not capability. It requires
installing process-wide tracing state before the first statement of the program: a listener pinned
to `AllDataAndRecorded` for the process lifetime, which overrides any sampler configured later, and
a second `ActivitySource` named `"CLI"` alongside the provider's. Because `Stop()` cannot fire
before the exporter exists, phases still need `SetEndTime()` now and `Stop()` later, and since
`SetEndTime()` does not restore `Activity.Current`, each phase must also reassign it or sequential
phases silently nest instead of becoming siblings (verified). Finally, configuration is unreadable
that early, so the `telemetry.enabled` opt-out is bypassed and has to be re-imposed by withholding
`Stop()` — replacing a gate that currently works by construction with one that works by
remembering to. Replay gives the same trace tree and keeps all of that for free.

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
