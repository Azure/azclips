# PowerShell Benchmark Telemetry Emission — Design

**Date:** 2026-08-20
**Status:** Approved, not yet implemented.
**Builds on:** `2026-08-18-benchmark-telemetry-design.md`, which did this for CLI benchmarks and
explicitly listed `PSBenchmarkBase` as out of scope. This change closes that gap.

## Problem

`PSBenchmarkBase` measures cmdlet invocation but suppresses telemetry:
`BenchmarkBase.RegisterBenchmarkStubs()` registers `StubTelemetryService`, which creates no exporter
and writes nothing. A PS benchmark therefore tells you *how long* `Get-AzResourceGroup` takes but not
*where that time goes*.

The CLI benchmarks now emit real telemetry. PS benchmarks should too.

Two things make PS harder than CLI:

1. **The PS module owns the sender.** `ModuleInitializer.OnImport()` registers
   `DirectTelemetrySender` and then calls `AzclipsServiceProvider.Initialize()`. The benchmark's
   registrations are queued *before* the import runs, and DI last-wins semantics mean the module
   clobbers them; registering afterwards throws. So the benchmark cannot substitute a sender at all
   today.
2. **The measured region is much smaller.** A CLI benchmark times `Program.Main`. A PS benchmark
   times only `ps.Invoke()` — the runspace and module import are deliberately excluded. Cost that
   CLI can absorb is proportionally far more visible here.

## Goals

Emit span-level telemetry from PowerShell benchmark runs into the per-run artifacts directory, in the
same shape the CLI benchmarks already produce.

**During a run**, traces are written under a per-benchmark staging root, split by stage:

```
artifacts/Benchmark/telemetry/<benchmark>/warmup/traces-<traceId>.log
artifacts/Benchmark/telemetry/<benchmark>/actual/traces-<traceId>.log
```

Each directory holds one `traces-<traceId>.log` per iteration, exactly as CLI does. Warmup traces are
written for real so warmup and measured iterations do the same work, then deleted unread. Only
`actual/` is consumed.

**After the run**, `actual/` is sampled down and collapsed into one file per benchmark method — e.g.
`PS.ResourceGroupBenchmarks.ListResourceGroups.jsonl` — which `Program.Main` moves into the
timestamped run folder. The raw directories are deleted. This reuses `TraceSampler` and the existing
`CollectTelemetry` step unchanged.

**The hard requirement: the file write must not cross the stopwatch.** Production PowerShell uses
`DirectTelemetrySender`, whose OTLP and Azure Monitor exporters batch onto a background thread, so a
cmdlet never waits on export. Benchmarks must preserve that. The measured region may pay for span
creation and tagging — it does in production — but not for I/O.

**Out of scope.** Startup benchmarks (`StartupBenchmarkBase`) are untouched; they spawn real
processes that keep writing to `%LocalAppData%/.azclips/traces`. So is any tooling that *consumes*
these files.

## Key decisions

### `FileTelemetrySender` is not reused; its exporter is

The obvious reading of "make benchmarks use the file sender" does not survive contact with the
requirement above. `FileTelemetrySender` wires `SimpleActivityExportProcessor`, which exports
**synchronously on the thread that ends the activity** — precisely the cost being excluded. It also
resolves its directory once, inside `ConfigureExporter`.

So the benchmark supplies its own `ITelemetrySender` that wires the *same production `FileExporter`*
behind OpenTelemetry's `BatchActivityExportProcessor`. Identical bytes on disk; the enqueue happens on
the cmdlet thread, the write does not.

This is production parity for PowerShell, which is the parity that matters here: prod PS batches, so
benchmark PS batches.

### The `TracerProvider` is built in `IterationSetup`, deliberately

Building a `TracerProvider` is expensive, and the DI container is rebuilt every iteration
(`IterationCleanup` disposes it, the next `Import-Module` re-initialises it). If the provider is
built lazily inside the measured region, **every measured iteration pays that construction** — far
more than the file write this change is avoiding.

`IterationSetup` therefore resolves `ITelemetryService` explicitly after `Import-Module`, forcing the
build before the stopwatch starts. Today this only happens by luck: `ImportTelemetry.EmitInBackground`
resolves the service on a `Task.Run`, which may or may not win the race.

### Module-import traces are dropped

Every iteration runs `Import-Module`, so `ImportTelemetry.EmitInBackground` emits a separate trace per
iteration. Left alone, roughly half of `actual/` — and therefore half of the 100 sampled traces —
would be module import rather than the cmdlet under test.

They are dropped by a benchmark-registered `BaseProcessor<Activity>` that clears
`ActivityTraceFlags.Recorded`, the same mechanism `DefaultActivityNameFilterProcessor` already uses.
Both `SimpleActivityExportProcessor` and `BatchActivityExportProcessor` skip unrecorded activities.

Dropping is by **tag, not name**. The import trace's root is `PowerShell.Activity.Root` — the same
name a cmdlet root uses — so a name filter cannot separate them. `ImportTelemetry.Emit` tags its root
with `TelemetryTags.ImportKind`, and that tag is the only reliable discriminator. The children are
dropped by the `PowerShell.Startup` name prefix.

This depends on `ImportKind` surviving `DefaultTelemetryPropertyFilterProcessor`, which runs first and
deletes any tag outside its allowlist. `ImportKind` is currently on that allowlist. If it were ever
removed, the import traces would silently reappear in the sample, so the end-to-end assertion below
covers it.

Suppressing the emit itself was rejected: it would also remove the pre-warm side effect the design
depends on, and it would need a production seam. Dropping at export keeps production untouched.

### Everything is per-iteration, and the drain happens in `IterationCleanup`

Nothing telemetry-related outlives an iteration. The `TracerProvider`, the batch processor, and the
exporter are all built in `IterationSetup` and destroyed in `IterationCleanup`, alongside the runspace
and the imported module that are already rebuilt every iteration.

That falls out of the existing teardown rather than fighting it. `IterationCleanup` disposes the DI
container, which disposes `TelemetryService` (`ITelemetryService : IDisposable`), which disposes the
`TracerProvider`. OpenTelemetry's dispose shuts down the processor chain, and shutting down a
`BatchActivityExportProcessor` **drains its queue to the exporter before returning**.

So the write still happens, and it happens in `IterationCleanup` — which BenchmarkDotNet does not
time. The measured region pays only the enqueue. The requirement is met by the existing lifecycle,
with no shared state and no lifetime tricks:

```
IterationSetup (untimed)   build provider + batch processor + exporter
ps.Invoke()    (TIMED)     span ends -> enqueue -> return
IterationCleanup (untimed) dispose container -> drain queue -> file written
```

The cost is one background thread created and destroyed per iteration, entirely outside the measured
region.

This also removes the need to tune the batch queue. The drain is triggered by shutdown, not by the
5-second scheduled flush, so the default delay never applies; the default queue size only has to hold
one cmdlet invocation's spans, which it does comfortably.

### The stage is latched during the measured region, sticky toward `actual`

The provider is built in `IterationSetup`, which always runs before the measured region, so asking
"warmup or measured?" at build time always answers "warmup" — `actual/` would be empty. And the
export runs at cleanup, by which point the engine has already signalled the measured region closed, so
asking at export time always answers "warmup" too.

The answer is only knowable *during* the iteration. A small `StageLatch` processor reads
`BenchmarkStageListener.IsMeasured` at `OnEnd`, on the cmdlet's own thread, and latches it:

```csharp
public override void OnEnd(Activity data)
{
    if (!data.Recorded) return;
    if (_stage.IsMeasured) _destination.MarkMeasured();  // set-only, never cleared
}
```

Set-only is what makes it correct. Every span in one iteration belongs to the same stage, so a single
span observed inside the measured region proves the whole iteration is measured; in a warmup iteration
no span ever observes `IsMeasured == true`. The exporter reads the latch once, at drain time, and
picks its directory.

The `data.Recorded` guard matters because `ImportTelemetry.EmitInBackground` emits on a background
thread whose timing is not controlled — it could end a span while the measured region is open. The
import filter has already cleared `Recorded` by the time `StageLatch` runs, so those spans cannot
latch the stage. The set-only rule covers any other stray span the same way.

Cost inside the measured region: one volatile read, plus the enqueue.

## Architecture

### Production change — one registration moved

`src/PS/Login/ModuleInitializer.cs`:

```csharp
// A default the host may replace. Prepends run before the framework defaults and before any
// override, so a host that registers its own sender wins.
AzclipsServiceProvider.PrependRegistration(services =>
    services.AddSingleton<ITelemetrySender, DirectTelemetrySender>());

AzclipsServiceProvider.OverrideRegistration(services =>
{
    services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
    services.AddSingleton<ITelemetryProvider, PSTelemetryProvider>();
    services.AddSingleton<IUserAgentHandler, PSUserAgentHandler>();
});
```

`AzclipsServiceProvider.Initialize` applies prepends, then the framework defaults, then overrides in
enqueue order. Moving only the sender to the prepend phase means:

- **In production**, nothing else registers `ITelemetrySender`, so `DirectTelemetrySender` survives
  and PowerShell behaviour is unchanged.
- **In a benchmark**, the benchmark's `OverrideRegistration` runs in a later phase and wins
  uncontested — the same shape the CLI design used for `ITraceDirectoryProvider`.

Only the sender moves. `ITelemetryProvider` **must** stay an override: `RegisterTelemetryServices()`
registers `DefaultTelemetryProvider` among the defaults, so a prepended `PSTelemetryProvider` would be
clobbered by them.

This rests on the framework defaults never registering `ITelemetrySender`. That is load-bearing and is
locked by a test.

No other production file changes. `ITelemetrySender`, `DirectTelemetrySender`, `FileTelemetrySender`,
`FileExporter`, `PSTelemetryProvider`, `TelemetryService`, and `ImportTelemetry` are all untouched.
`Core.Telemetry.csproj` already carries `InternalsVisibleTo Include="Benchmark"`, so the benchmark can
use the internal `FileExporter` without widening any surface.

### New benchmark types — `tests/Benchmark/Telemetry/`

**`BenchmarkFileTelemetrySender : ITelemetrySender`** — the benchmark's sender. Holds the two
directory paths and the stage listener. `ConfigureExporter` builds a fresh chain per call:

```csharp
public void ConfigureExporter(TracerProviderBuilder builder)
{
    var exporter = new StageDirectedFileExporter(_warmupDirectory, _actualDirectory);
    builder.AddProcessor(new StageLatch(_stage, exporter));
    builder.AddProcessor(new BatchActivityExportProcessor(exporter));
}
```

Everything it creates is owned by the `TracerProvider` and dies with it. This is `FileTelemetrySender`
with two substitutions: `BatchActivityExportProcessor` instead of `SimpleActivityExportProcessor`, and
a stage-directed exporter instead of a fixed directory.

**`StageDirectedFileExporter : BaseExporter<Activity>`** — constructs the production `FileExporter`
lazily on first export, against whichever directory the latch selects. One exporter, one trace file
per iteration.

```csharp
public override ExportResult Export(in Batch<Activity> batch) =>
    (_inner ??= new FileExporter(_measured ? _actualDirectory : _warmupDirectory)).Export(batch);
```

**`StageLatch : BaseProcessor<Activity>`** — sets the exporter's measured flag when a recorded span
ends inside the measured region. Set-only; see the decision above.

**`ImportTraceFilter : BaseProcessor<Activity>`** — clears `Recorded` when the activity carries
`TelemetryTags.ImportKind` or its name starts with `PowerShell.Startup`. Registered as an additional
`BaseProcessor<Activity>`; `PSTelemetryProvider` adds every DI-registered processor to the builder
*before* calling `sender.ConfigureExporter`, so the drop happens upstream of the latch and the queue.

**`BenchmarkTelemetryArtifacts`** — the staging-root, telemetry-root, and benchmark-name resolution
plus `Prune()` and `Reset()`, extracted from `CLIBenchmarkBase` so both bases share one copy.
`CLIBenchmarkBase` is updated to use it; its behaviour is unchanged.

### `PSBenchmarkBase` changes

`GlobalSetup` — after the existing module staging: resolve the benchmark name, reset the telemetry
root, construct the `BenchmarkStageListener` and the `BenchmarkFileTelemetrySender`. Both are
stateless with respect to a single iteration — the listener tracks the engine's stage, and the sender
just holds two paths — so neither needs rebuilding per iteration.

`IterationSetup` — `RegisterBenchmarkStubs()`, then `RegisterTelemetryCapture()`, then the existing
runspace open and `Import-Module`, then the pre-warm:

```csharp
private void RegisterTelemetryCapture()
{
    AzclipsServiceProvider.OverrideRegistration(services =>
    {
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<ITelemetrySender>(_sender);
        services.AddSingleton<BaseProcessor<Activity>, ImportTraceFilter>();
    });
}
```

Registered every iteration because `IterationCleanup` disposes the container. `TelemetryService`'s
constructor takes `(ITelemetryProvider, IConfigurationService, ClientType)`, all already registered —
`ClientType` is seeded as `PowerShell` by `Initialize`.

`IterationCleanup` — unchanged. The existing `CleanupCommon()` call is what drains the queue, via the
dispose chain described above.

`GlobalCleanup` (new) — dispose the stage listener, prune `actual/` into `<benchmark>.jsonl`, delete
the raw directories. No draining to do: every iteration already flushed its own. Never throws.

### Data flow, one iteration

```
IterationSetup (untimed)
  register stubs -> register telemetry capture -> Import-Module -> resolve ITelemetryService
                                                  (prepends Direct,   (builds TracerProvider,
                                                   override wins)      batch processor, exporter)

ps.Invoke() (TIMED)
  span ends -> name filter -> property filter -> ImportTraceFilter
            -> StageLatch: read IsMeasured, latch -> BatchProcessor: enqueue -> returns

IterationCleanup (untimed)
  dispose container -> dispose TelemetryService -> dispose TracerProvider
                    -> batch shutdown drains queue -> FileExporter
                    -> <stage>/traces-<traceId>.log

GlobalCleanup (untimed, once)
  TraceSampler.Prune(actual/) -> <benchmark>.jsonl -> delete raw dirs
```

## Deliberate divergence from the CLI design

**`StageTraceDirectoryProvider` is not used by PS.** CLI builds its provider inside the measured
region, so resolving the directory at build time gives the right answer there. PS builds its provider
in `IterationSetup`, where that answer is always "warmup", so the decision moves to the `StageLatch`
instead. The type stays, unchanged, for CLI.

Otherwise the shape matches CLI: one `FileExporter` per iteration writing one `traces-<traceId>.log`,
roughly 10,000 transient files per benchmark method, deleted in that method's `GlobalCleanup`.

## Failure handling

A telemetry failure must never change the benchmark's exit code.

| Situation | Behaviour |
|---|---|
| Drain exceeds `TelemetryService.Dispose`'s 500 ms cap | That iteration's trace is truncated or lost. See below — this is the one risk the per-iteration model introduces. |
| Batch queue overflows | OTel drops silently. The default 2048 capacity is far above one cmdlet invocation's span count, and the queue starts empty every iteration. |
| Telemetry disabled in `~/.azclips/config.json` | No spans at all; `kept 0` with the existing message naming both possible causes. |
| Pruning fails (parse error, locked file) | `[Telemetry]` warning, exit code stays green. |
| `--benchmarkName` absent from the child command line | `GlobalSetup` throws rather than writing under a guessed name. |
| Stale `artifacts/Benchmark/telemetry/<benchmark>/` from a previous run | `GlobalSetup` deletes it first. |

**On the 500 ms cap.** `TelemetryService.Dispose()` runs `TracerProvider.Dispose()` on a `Task` and
waits at most 500 ms, to avoid a real command hanging on an unreachable exporter. Under this design
that wait is what the drain happens inside, so it becomes the deadline for writing an iteration's
trace.

It should not bind: a drain is a handful of spans and one `File.AppendAllText`, which is
sub-millisecond in the normal case. But it is not guaranteed, and a slow disk could clip a trace. The
consequence is a missing trace, not a corrupt one — `TraceSampler` groups by `TraceId` and a partial
file simply yields fewer traces. The end-to-end assertion below (exactly 5 traces from 5 measured
iterations) is what would surface it, and a full run tolerates losing a few of 5000 because the output
is sampled to 100 anyway.

This is accepted rather than worked around: the alternative is a production change to
`TelemetryService.Dispose` purely to serve benchmarks, which is worse.

## Consequences

PS benchmark means and `[MemoryDiagnoser]` numbers step up once, uniformly, because every iteration
now creates and tags real spans where `StubTelemetryService` did nothing. The step should be
materially smaller than CLI's, because no file I/O crosses the stopwatch — but it is not zero, and it
is not meant to be: span creation is production behaviour.

As with the CLI change, the App Insights historical series and the 115% regression gate will see a
one-time discontinuity on the run this lands. Numbers remain comparable run-to-run afterwards.

Production PowerShell behaviour is unchanged: the same `DirectTelemetrySender`, resolved through a
different registration phase.

## Testing

**Unit — production surface only.**

- `tests/Core.Telemetry.Tests`: `RegisterTelemetryServices()` registers no `ITelemetrySender`. If a
  default is ever added, PowerShell silently loses `DirectTelemetrySender`; this test is what catches
  it.
- `tests/Cmdlets.Tests`: the ordering contract. With the module's prepend applied, a later
  `OverrideRegistration` resolves to the overriding sender; with no override, it resolves to
  `DirectTelemetrySender`. There is existing precedent in that project for driving
  `AzclipsServiceProvider.Initialize`.

The new benchmark types are **not** unit-tested, following the precedent set for `TraceSampler` and
`BenchmarkName`: `tests/Benchmark` is an `Exe` with no companion test project, and adding one is not
justified by four small classes. They are covered end-to-end. The accepted gap is queue-overflow
behaviour under sustained load, which only a full 5000-iteration run would exercise.

**Build:** `dotnet build azclips.slnx`.

**Targeted:** `dotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj` and
`dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj`.

**End-to-end** — the `validate-benchmarks` 5-iteration run, asserting more than a smoke test normally
would:

- One `.jsonl` per PS benchmark method under `artifacts/Benchmark/<timestamp>/telemetry/`, each named
  after a row in the BDN summary.
- **Exactly 5 traces in each file — not 0, not 10.** This is the load-bearing assertion. 0 would mean
  the stage was read at provider-build time and everything landed in `warmup/`; 10 would mean the
  stage listener never fired.
- No `PowerShell.Startup` spans, and no root carrying `ImportKind` — proves `ImportTraceFilter`.
- Each root is `PowerShell.Activity.Root` tagged with the cmdlet — proves the real telemetry stack
  ran rather than `StubTelemetryService`.
- CLI `.jsonl` files still produced, still 5 traces each — proves the `BenchmarkTelemetryArtifacts`
  extraction did not regress CLI.
- Nothing left behind under `artifacts/Benchmark/telemetry/`.

**Performance check.** Capture PS benchmark means before and after on the same machine and record both
in the PR. The expectation is a small uniform step-up from span creation, with no I/O across the
stopwatch. A large move means the pre-warm is not taking effect and the provider is being built inside
the measured region.
