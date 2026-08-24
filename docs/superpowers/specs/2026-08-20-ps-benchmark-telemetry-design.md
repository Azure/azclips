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

Each directory holds a single append-only file rather than one per iteration — see *Deliberate
divergences* below. Warmup traces are written for real so warmup and measured iterations do the same
work, then deleted unread. Only `actual/` is consumed.

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
behind OpenTelemetry's `BatchActivityExportProcessor`. Identical bytes on disk; the write happens on
OTel's background thread.

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

### The stage is read at span end, not at provider build

This is the consequence of the decision above, and the core of the design.

CLI answers "warmup or measured?" via `StageTraceDirectoryProvider`, called from
`FileTelemetrySender.ConfigureExporter`. That works for CLI because the provider is built inside the
measured region. For PS the provider is built in `IterationSetup`, which is *always* outside the
measured region — so the answer would always be "warmup", and `actual/` would be empty.

The fix is to move the question to a moment where the answer is genuinely known: `OnEnd`, on the
cmdlet's own thread, inside the measured region. A routing processor reads
`BenchmarkStageListener.IsMeasured` there — one volatile read — and forwards the activity to the
batch processor for the matching directory. The background thread then writes to a directory that was
already decided, so a drain that lands after the iteration ended still files correctly.

Resolving the directory inside `FileExporter.Export` instead was rejected: that runs on the writer
thread at an arbitrary later time, which makes attribution racy in exactly the way this avoids.

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

### The background writers must outlive the iteration that created them

The two batch processors own the background writer threads. They need to survive the whole benchmark
— all 10,000 iterations — and drain once at the end.

But every iteration destroys its telemetry chain. `IterationCleanup` disposes the DI container, which
disposes `TelemetryService`, which disposes the `TracerProvider`. OpenTelemetry's dispose then walks
the provider's processor chain and shuts down every processor in it.

So attaching the shared router directly is self-defeating: the first iteration would shut down the
writer threads, and every iteration after it would write nothing.

`ConfigureExporter` therefore attaches a fresh `NonDisposingProcessor` each iteration — a thin
stand-in that passes spans *inward* to the shared router but does not pass shutdown or dispose
*outward* to it. Each iteration disposes its own throwaway wrapper; the router, its two batch
processors, and its two `FileExporter`s survive, and `GlobalCleanup` drains them once.

The CLI design considered a proxy like this and rejected it as not worth the trouble — fairly, since
CLI builds a fresh exporter per iteration and has nothing that needs to survive. Here nothing works
without it.

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

**`StageRoutingTelemetrySender : ITelemetrySender`** — the benchmark's sender. Holds the shared
router; `ConfigureExporter` adds a fresh `NonDisposingProcessor` wrapping it.

**`StageRouter : BaseProcessor<Activity>`** — one per benchmark process. Owns two
`BatchActivityExportProcessor`s, each wrapping a `FileExporter` pointed at `warmup/` or `actual/`.

```csharp
public override void OnEnd(Activity data) =>
    (_stage.IsMeasured ? _actual : _warmup).OnEnd(data);
```

`OnForceFlush`, `OnShutdown`, and `Dispose` forward to both children. Queue sizing is set explicitly
rather than left at OTel's defaults (2048 queued, 5 s flush interval), which are tuned for interactive
command rates, not a benchmark loop. The constraint: the queue must absorb one iteration's burst
without the writer thread ever becoming the bottleneck, across a 5000-iteration stage. Concretely,
size it to hold several thousand spans and shorten the flush interval to roughly a second, so the
writer drains continuously instead of in 5-second surges.

**`NonDisposingProcessor : BaseProcessor<Activity>`** — the per-iteration stand-in that stops
`TracerProvider.Dispose()` from tearing down the shared router. Forwards `OnStart`/`OnEnd` to an
inner processor; `OnShutdown` returns `true` without shutting the inner one down, and `Dispose(bool)`
is a no-op.

**`ImportTraceFilter : BaseProcessor<Activity>`** — clears `Recorded` when the activity carries
`TelemetryTags.ImportKind` or its name starts with `PowerShell.Startup`. Registered as an additional
`BaseProcessor<Activity>`; `PSTelemetryProvider` adds every DI-registered processor to the builder
*before* calling `sender.ConfigureExporter`, so the drop happens upstream of routing.

**`BenchmarkTelemetryArtifacts`** — the staging-root, telemetry-root, and benchmark-name resolution
plus `Prune()` and `Reset()`, extracted from `CLIBenchmarkBase` so both bases share one copy.
`CLIBenchmarkBase` is updated to use it; its behaviour is unchanged.

### `PSBenchmarkBase` changes

`GlobalSetup` — after the existing module staging: resolve the benchmark name, reset the telemetry
root, construct the `BenchmarkStageListener` and the `StageRouter`.

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

Registered every iteration because `IterationCleanup` disposes the container; the sender instance is
the same throughout. `TelemetryService`'s constructor takes `(ITelemetryProvider, IConfigurationService,
ClientType)`, all already registered — `ClientType` is seeded as `PowerShell` by `Initialize`.

`IterationCleanup` — unchanged.

`GlobalCleanup` (new) — dispose the stage listener, dispose the router (which drains both queues),
prune `actual/` into `<benchmark>.jsonl`, delete the raw directories. Never throws.

### Data flow, one iteration

```
IterationSetup (untimed)
  register stubs -> register telemetry capture -> Import-Module -> resolve ITelemetryService
                                                  (prepends Direct,   (builds TracerProvider)
                                                   override wins)

ps.Invoke() (TIMED)
  span ends -> name filter -> property filter -> ImportTraceFilter -> NonDisposingProcessor
            -> StageRouter.OnEnd: read IsMeasured, enqueue -> returns

OTel batch thread (untimed)
  drain -> FileExporter -> actual/traces-<id>.log

IterationCleanup (untimed)
  dispose container -> TracerProvider disposed -> proxy swallows it -> router survives

GlobalCleanup (untimed, once)
  dispose router -> drain -> TraceSampler.Prune -> delete raw dirs
```

## Deliberate divergences from the CLI design

**One appended trace file per stage, not one per iteration.** CLI constructs a `FileExporter` per
iteration, so each iteration writes its own `traces-<traceId>.log`. Here the exporters are shared, and
`FileExporter` caches its path from the first activity it sees, so each directory ends up with a
single appended file. `TraceSampler` groups by `TraceId`, so the sampled output is identical — and it
avoids ~10,000 transient files per benchmark method. CLI's file-per-iteration fidelity argument was
about modelling a real `az` process's cost; that argument does not apply once the write is off the
measured thread.

**`StageTraceDirectoryProvider` is not used by PS.** The stage decision moved into the router. The
type stays, unchanged, for CLI.

## Failure handling

A telemetry failure must never change the benchmark's exit code.

| Situation | Behaviour |
|---|---|
| Router drain throws or times out on dispose | Caught in `GlobalCleanup`, printed as `[Telemetry] <name>: ...`. Runs untimed, after the last iteration. |
| Batch queue overflows | OTel drops silently. Mitigated by sizing the queue above one run's span count and shortening the flush interval. The observable signal is the existing `kept N sampled trace(s)` line. |
| Telemetry disabled in `~/.azclips/config.json` | No spans at all; `kept 0` with the existing message naming both possible causes. |
| Pruning fails (parse error, locked file) | `[Telemetry]` warning, exit code stays green. |
| `--benchmarkName` absent from the child command line | `GlobalSetup` throws rather than writing under a guessed name. |
| Stale `artifacts/Benchmark/telemetry/<benchmark>/` from a previous run | `GlobalSetup` deletes it first. |

`TelemetryService.Dispose()` caps `TracerProvider.Dispose()` at 500 ms on every iteration. Because the
proxy swallows shutdown, that cap can never truncate benchmark data — the real drain happens once, in
`GlobalCleanup`.

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
