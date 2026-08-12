# PowerShell Module Import Telemetry — Design

**Date:** 2026-08-12
**Status:** Approved
**Scope:** `src/PS/Login`, `src/Core/Core.Telemetry`, `src/CLI/Telemetry` (move only)

## Problem

The PowerShell client has no visibility into module import. `ModuleInitializer.OnImport`
registers the PS-specific services and builds the DI container, and none of that is measured.

Measurements taken against the built module on Windows:

| Scenario | `Import-Module Login` | `Import-Module Az` (rollup) |
| --- | --- | --- |
| Machine-cold (first after boot) | 70 ms | 98 ms |
| Process-cold (fresh pwsh, OS cache warm) | 21–24 ms | 67–77 ms |
| Warm re-import (same process) | 15.4 ms → 5.2 ms | — |

A second measurement reframes the problem. Resolution cost is **order-dependent**: whichever
service is resolved first absorbs the shared DI graph construction and JIT warm-up, so a
figure is only meaningful alongside the order it was measured in.

Resolved in the order a cmdlet uses them — `IEngine` in `BeginProcessing`, then
`ITelemetryService` in `ProcessRecord`:

| Operation | First resolve | Second resolve |
| --- | --- | --- |
| `IEngine` | 76.8 ms | 1.3 ms |
| `ITelemetryService` | 3.0 ms | 1.0 ms |

Resolved in the order the background emit uses them, immediately after import with nothing
else constructed:

| Operation | Cost |
| --- | --- |
| `IConfigurationService` | 36.2 ms |
| `ITelemetryService` | 83.8 ms |

The same `ITelemetryService` therefore costs 3 ms or 83.8 ms depending purely on what ran
before it. The second-resolve column confirms these are one-time construction costs rather
than measurement noise.

End to end, the user-visible effect is larger than any single service suggests:

| Stage | Cost |
| --- | --- |
| `Import-Module Login` | 38.9 ms |
| First cmdlet | 359.5 ms |
| Second cmdlet | 14.8 ms |
| Third cmdlet | 13.3 ms |

The first cmdlet pays roughly 345 ms more than steady state. This is not attributable to one
service: it is the whole object graph being constructed lazily, with `IEngine` the largest
single identifiable piece. Addressing it is out of scope here; see
[Follow-ups](#follow-ups).

## Constraint: instrumenting import naively would dominate what it measures

Emitting a span requires `ITelemetryService`. At import nothing else has been resolved, so it
would be the first resolve and would absorb the shared construction cost — 83.8 ms after an
explicit `IConfigurationService` resolve, or 109.7 ms when resolved cold with no prior
resolution at all. Against a 21.6–38.9 ms import, instrumenting `OnImport` in the obvious way
would roughly quadruple import time and report a figure inflated by its own instrumentation.

This is the CLI's chicken-and-egg problem inverted. In the CLI, replay was necessary because
the telemetry stack did not yet exist. In PowerShell it *can* be built during import, but
building it destroys the measurement. The remedy is the same: record cheap timestamps during
import, and emit them once the telemetry stack is being constructed anyway.

## Approach: record during import, emit from a background thread

`OnImport` records timestamps — effectively free — and hands the emit to a background task.
The following was verified with a purpose-built probe module: `OnImport` returned in 1.1 ms
while 120 ms of simulated work ran to completion on a separate thread.

This keeps import at its current cost, confines the change to the module's own code, and, as
a side effect, pre-warms the telemetry stack so the first cmdlet no longer pays the roughly
120 ms of configuration and telemetry construction.

### Why not the alternatives

**Emit synchronously during import.** Correct and simple, and the only option that survives a
process exiting immediately after import. Rejected because it takes import from 21.6 ms to
roughly 140 ms — quadrupling the measured quantity.

**Replay on the first cmdlet.** Import stays fast and no background thread is involved, but
the trigger needs a home. There are 14 identical `StartRootActivity()` call sites across the
PS cmdlets with no shared base beyond `ConfigCmdletBase`, so the trigger would have to live in
the shared `TelemetryService` — pushing a PowerShell-shaped concern into common code.

Both deferred options share one gap, so it did not discriminate between them: if the process
exits immediately after import, nothing is emitted. This was verified — the background task
starts and is killed at process exit. Such a session runs zero cmdlets and therefore emits no
telemetry today either.

## What is measured

The `OnImport` window: the PS service registrations plus `AzclipsServiceProvider.Initialize`.
This is real work on a warm import as well as a cold one — `OnRemove` disposes the container,
so a re-import rebuilds it.

### What is not measured, and why

PowerShell's module load order was established with a probe module exercising every hook.
Elapsed times are from a 159 ms toy import and are indicative of ordering, not of the real
module's cost:

```
=== Import-Module begin ===
  +83 ms   ScriptsToProcess.ps1        earliest in-module hook
  +46 ms   RootModule script START
  +15 ms   static ctor                 assembly first touched
  +0.8 ms  OnImport                    the hook this design uses
  +0.8 ms  RootModule END
  +13 ms   === Import-Module done ===  cmdlet export processing
```

`OnImport` sits near the end of the sequence, so this design does not capture assembly
loading, manifest processing, or cmdlet export processing. A `ScriptsToProcess` hook would
push the start anchor earlier and capture assembly loading, at the cost of one `.ps1` file and
one manifest line per module. It is deliberately deferred; see [Follow-ups](#follow-ups).
Nothing in the design precludes it — an earlier anchor only moves the root span's start time
and changes no shape.

## Trace shape

Emitted as its own trace, from the background thread:

```
PowerShell.Activity.Root                    importKind = cold | warm
└── PowerShell.Performance
    └── PowerShell.Performance.Container    the DI container build
```

**Its own trace, not nested under a cmdlet.** Import precedes every cmdlet, so attaching
backdated phases to a cmdlet's root would produce a child that starts before its parent — the
one backdating case that renders incorrectly in Jaeger and Application Insights. Backdating a
*root* is safe, because no child can precede it.

**The root is `PowerShell.Activity.Root`.** Reusing `StartRootActivity` keeps the invariant tag
enrichment — installation id, product name and version, schema version, client type. An import
trace is identifiable by its `PowerShell.Performance.*` subtree.

**Phase names mirror the CLI.** `PowerShell.Performance.Container` measures the same work as
`CLI.Performance.Container`, so container build cost is directly comparable across clients.

**The root's end time must be set explicitly.** In the CLI the root activity is disposed at the
natural end of the work it covers, so its duration is correct without intervention. Here the
root is created on the background thread *after* import has finished, so disposing it normally
would report a duration spanning import start to emit time — inflated by the background
thread's scheduling delay and by the telemetry construction being measured against it. `Emit`
therefore calls `SetEndTime` with the recorded import end. The same applies to the enclosing
`PowerShell.Performance` span, which `Replay` already handles.

## Components

### `PerformanceTimeline` moves to `Core.Telemetry` and is shared

The CLI's `PerformanceTimeline` already does exactly what PowerShell needs. The CLI's usage is:

```csharp
using var activity = telemetryService.StartRootActivity(performance.StartedAt);
performance.Replay(telemetryService);
```

PowerShell makes the same two calls; every difference — its own root, the `importKind` tag, the
background thread — lives at the call site, not in the timeline. The type itself is unchanged
in behaviour.

Sharing rather than duplicating matters because the difficult parts of this type — stack-based
nesting, the out-of-order disposal guard, and the leaked-handle `End` fallback — were each the
subject of review findings during the CLI work. A second copy would reproduce those defects.

| Change | Detail |
| --- | --- |
| Move | `src/CLI/Telemetry/PerformanceTimeline.cs` → `src/Core/Core.Telemetry/PerformanceTimeline.cs` |
| Namespace | `Core.Cli.Telemetry` → `Core.Telemetry` |
| Visibility | `internal sealed` → `public sealed` (the PS module is a separate assembly) |
| Parameterise | `EnclosingSpanName` becomes a constructor argument — the only behavioural change |
| Update | the CLI's single construction site and its `using` directive |
| Move tests | `tests/Cli.Tests/Telemetry/PerformanceTimelineTests.cs` → `tests/Core.Telemetry.Tests/` |

`Core.Telemetry` is already reachable from the Login assembly; `ModuleInitializer.cs` imports
that namespace today.

### `ImportTelemetry` — new, `src/PS/Login/Telemetry/ImportTelemetry.cs`

Sits beside `PSTelemetryProvider` and owns everything specific to import measurement:

- `NextImportKind()` — returns `"cold"` on the first import in the process and `"warm"`
  afterwards, using a counter advanced with `Interlocked.Increment`. Verified: static state
  survives `Remove-Module` followed by `Import-Module` — across a remove/re-import cycle the
  static constructor ran once while the counter advanced from 1 to 2. Named as a verb because
  it mutates that counter; it is called exactly once per import.
- `Emit(ITelemetryService, PerformanceTimeline, string importKind, DateTime importEnd)` —
  synchronous and pure, so it is unit-testable against a recording stub. Starts the backdated
  root, tags it with `importKind`, calls `Replay`, and closes the root at `importEnd`.
- `EmitInBackground(PerformanceTimeline, string importKind, DateTime importEnd)` — the
  fire-and-forget wrapper that checks the opt-out, resolves the telemetry service, and calls
  `Emit`.

Splitting `Emit` from `EmitInBackground` is what makes the feature testable at all; it is the
same split that gave `PerformanceTimeline` meaningful coverage in the CLI work.

### `ModuleInitializer.OnImport` — modified

Creates the timeline, measures `Initialize` inside a `Measure("PowerShell.Performance.Container")`
block, then starts the background emit. Roughly six lines added.

### Allowlists — modified

Both telemetry filters are deny-by-default, so without these entries the spans are dropped
silently:

- `DefaultTelemetryActivityNameFilter` — add `PowerShell.Performance` as an exact name and
  `PowerShell.Performance.` as a prefix.
- `TelemetryTags` — add `ImportKind = "importKind"`.
- `DefaultTelemetryPropertyFilter` — allow the new key.

## Behaviour

**The opt-out is honoured before anything is built.** With `telemetry.enabled=false`,
`StartActivity` returns null and nothing is emitted — but the background thread would still
have constructed the full pipeline, including an Azure Monitor exporter, for a user who opted
out. The background path therefore reads `TelemetryOptions.Enabled` through
`IConfigurationService` and returns before resolving `ITelemetryService`.

**Failures never escape.** The entire background block is wrapped and its exceptions swallowed.
Telemetry must never break `Import-Module`.

**Pre-warming is intended, and has a cost.** Because the background thread constructs the
telemetry stack, the first cmdlet no longer pays the roughly 120 ms of configuration and
telemetry construction. The corollary is that a session which
runs no cmdlets at all builds a pipeline nobody uses. That is the accepted trade of this
approach.

**Concurrency.** The background resolve may race a cmdlet resolving the same singleton;
`Microsoft.Extensions.DependencyInjection` locks around singleton construction, so this is
safe.

**Accepted gap.** Nothing is emitted if the process exits immediately after import.

**The background resolution is deliberately not measured.** `EmitInBackground` resolves
`IConfigurationService` (36.2 ms) and `ITelemetryService` (83.8 ms) — roughly 120 ms of work,
three times the import being reported — and none of it appears in the trace. This is a
considered choice, not an oversight, and it was raised and rejected during design review.

Measuring it is straightforward: wrap both resolves in a
`timeline.Measure("PowerShell.Performance.Services")` block before calling `Emit`. The
recorded timestamps are only data, so measuring telemetry's own construction and then using
telemetry to emit that measurement works, exactly as the CLI does with
`CLI.Performance.Services`.

It was rejected because of what it does to the root span. The `Services` phase runs on the
background thread *after* `OnImport` has returned, so a root enclosing it would span import
start through telemetry readiness rather than the import window — and the root is the span a
reader naturally reads as "the import". Keeping the root to the import window costs visibility
into the warm-up but keeps the headline number honest. Should this be revisited, note that the
120 ms overlaps whatever the user does next rather than delaying it, so it is not latency the
user waits on.

**Disposing the container mid-emit is safe but silent.** `OnRemove` calls
`AzclipsServiceProvider.Dispose()`. A `Remove-Module` issued immediately after
`Import-Module` can therefore dispose the container while the background thread is still
resolving, producing an `ObjectDisposedException` on that thread. The surrounding `catch`
swallows it, so the failure mode is a missing import trace rather than an error surfaced to
the user.

## Testing

`Replay` remains synchronous and pure, so the existing `PerformanceTimelineTests` move to
`Core.Telemetry.Tests` largely intact and continue to cover nesting, durations, out-of-order
disposal, the leaked-handle fallback, and the telemetry-disabled path. Added coverage:

- the enclosing span name is genuinely parameterised — a CLI-named and a PS-named timeline each
  emit their own enclosing span
- `NextImportKind()` reports `cold` on the first call and `warm` on subsequent ones
- `Emit` produces a root carrying `importKind` and a `PowerShell.Performance.Container` child
  whose duration matches the recorded phase
- `Emit` closes the root at the supplied import end rather than at emit time — assert the root's
  duration matches the recorded import window and does not absorb an artificial delay between
  recording and emitting, which is the regression the explicit `SetEndTime` exists to prevent
- `Emit` does not throw when the telemetry service returns null for every span
- `PowerShell.Performance` and `PowerShell.Performance.Container` pass
  `DefaultTelemetryActivityNameFilter.IsAllowed`; `importKind` passes the property filter

Manual verification, since PowerShell exports directly rather than buffering to disk: Debug
builds resolve to the `Local` target, so start the local collector with
`docker compose -f tools/observability/docker-compose.yml up`, import the Login module in a
fresh `pwsh`, and confirm the trace and its shape in Jaeger.

## Prerequisite

The working tree carries an uncommitted edit to `DefaultTelemetryActivityNameFilter.IsAllowed`
that short-circuits it to `return true;`, disabling the deny-by-default activity-name filter.
This must be reverted before the work lands. While it is in place the allowlist additions
cannot be meaningfully tested, and the filter offers no guarantee.

## Follow-ups

Out of scope here, recorded so the reasoning is not lost:

1. **Earlier start anchor.** A `ScriptsToProcess` hook would capture assembly loading in
   addition to the DI container build. Costs one `.ps1` and one manifest line per module.
2. **Measure first-cmdlet service resolution.** The first cmdlet costs roughly 345 ms more
   than steady state (359.5 ms against 14.8 ms) — nearly nine times the import this design
   instruments, and entirely invisible today. `IEngine` at 76.8 ms is the largest single
   piece; the remainder is the object graph it pulls in. This is the biggest unmeasured cost
   in the PowerShell client.
3. **Measure the background warm-up.** See the Behaviour section: roughly 120 ms of service
   construction runs unmeasured on the background thread. Adding a
   `PowerShell.Performance.Services` phase would capture it, at the cost of the root span no
   longer representing the import window.
4. **Rollup coverage.** Only the Login module has a `ModuleInitializer`. `Import-Module Az`
   pulls in Compute, Resources, and Config as well; their import cost stays invisible.

Items 2 and 3 share groundwork with this design: `PerformanceTimeline` in `Core.Telemetry`
and the `PowerShell.Performance.` allowlist prefix are what either would build on.
