# Startup-Time Benchmarking (CLI + PowerShell) — Design

**Status:** Approved (design)
**Date:** 2026-06-23
**Surfaces:** CLI (`azv2`) and PowerShell (`pwsh` + Cirrus modules)
**Scope of this spec:** Process-level cold/warm startup timing of a built-in command, stopping
at the moment the command processor has been resolved but *before* it executes.

---

## 1. Goal & Motivation

Measure the **real, end-to-end startup cost** a user pays when invoking a bundled (built-in)
command on both the CLI and PowerShell surfaces, and track it over time as a permanent,
CI-integrated benchmark alongside the existing `tests/Benchmark` suite.

"Bundled extensions" here means the **true-core built-ins** seeded in
`Core.Extensions.BuiltInExtensions.Map`: `extension`, `login`, and `config`. This first
iteration benchmarks **`login`** and **`config`** because they are present in every install and
exercise the full assembly-load + index-resolution path without requiring network or auth.

### The finish line (the "seam")

A command invocation flows through `Core.Engine.ExecutionSession`:

```
RunAsync()           : await (await GetProcessor()).ProcessAsync();
Run()  (PS path)     : ExecuteProcessorAsync() -> await (await GetProcessor()).ProcessAsync();
```

`GetProcessor()` performs all startup-relevant work:

- `CreateProcessorContext(...)` — auth context (read-only), HTTP pipeline construction
- `GetExtensionIndex()` — **loads the built-in extension assembly** (login/config) into an
  `AssemblyLoadContext` and resolves its `IExtensionIndex`
- resolves `CommandMetadata`, instantiates the processor, `Init(...)`, binds arguments

`ProcessAsync()` is the **execution** step that fires the Azure API call.

**The benchmark's finish line is exactly between these two calls**: after the processor is fully
resolved, before it executes. Both the CLI and PowerShell surfaces funnel through
`ExecutionSession`, so a single hook covers both. Because `GetProcessor()` performs no network or
token acquisition (the token acquirer is only *wired* as a delegate, invoked later inside
`ProcessAsync`), the seam is reachable **logged-out with zero Azure dependency**.

---

## 2. What "cold" vs "warm" mean here

Because every measured launch is a **fresh OS process**, the in-process caches
(`--no-cache` / `CIRRUS_NO_EXTENSION_CACHE` assembly-load-context cache) start empty on every
launch regardless and therefore have **no cross-process effect**. The `~/.cirrus` versioned
extension cache is also irrelevant for built-ins, which load from the app's `Extensions/`
directory seeded in-memory by `BuiltInExtensions`.

The **only** real cold/warm lever at process scope is whether the .NET runtime DLLs, the `azv2`
assemblies, and the login/config extension DLLs are served from **disk (cold)** or the **OS page
cache in RAM (warm)**. JIT is re-paid on every launch in both modes (the CLI ships plain IL today
— no ReadyToRun/AOT), so JIT is a constant, not a cold/warm differentiator.

| Stage (per launch) | Cold | Warm |
|---|---|---|
| OS process create | yes | yes |
| .NET runtime init (CoreCLR/hostfxr) | yes | yes |
| Assembly load from **disk** | **yes (disk)** | no (**RAM page cache**) |
| JIT of startup IL | yes | yes |
| DI + parse + load login/config + resolve processor | yes | yes |

- **Warm start** = BenchmarkDotNet steady-state: a fresh process per `[Benchmark]` invocation,
  but BDN's warmup launches have already pulled the DLLs into the OS page cache, so measured
  launches hit hot caches. Low variance.
- **Cold start** = a separate job that **evicts the OS page cache before each measured launch**,
  so every recorded launch re-reads from disk — the "first run after reboot" experience. Higher
  variance.

Relationship: `cold process > warm process > (in-process steady state)`. The in-process notion is
explicitly **out of scope** for this spec.

---

## 3. Approach

**Approach A — BenchmarkDotNet process job** (selected). Each `[Benchmark]` method does
`Process.Start(...)` of the real published binary + `WaitForExit()`. This reuses the existing
suite's runner, gating, gh-pages dashboards, AppInsights export, and chart generation with no new
plumbing.

Rejected alternatives:
- **B — custom launch harness**: more control over cold/warm semantics but reimplements
  statistics, outlier handling, and gating that BDN already provides.
- **C — phase-instrumented timings**: a future add-on for *diagnosis* (where a regression is), not
  needed for the core "how long does startup take" goal.

---

## 4. Measurement matrix

Initial tracked numbers: **{CLI, PS} × {login, config} × {cold, warm} = 8**.

| Surface | Launch | Probe command | Built-in exercised |
|---|---|---|---|
| CLI | `Process.Start(artifacts/<Config>/Core.Cli/azv2[.exe] config list)` | `config list` | Config |
| CLI | `... login show` | `login show` | Login |
| PS  | `pwsh -NoProfile -Command "Import-Module <staged Config .psd1>; Get-CirrusConfigList"` | `Get-CirrusConfigList` | Config |
| PS  | `pwsh -NoProfile -Command "Import-Module <staged Login .psd1>; <login cmdlet>"` | login cmdlet | Login |

All four launches run with `CIRRUS_BENCH_RESOLVE_ONLY=1` in the child process environment, so each
stops at the seam regardless of the specific subcommand. PowerShell numbers legitimately include
`pwsh` startup + module import (which triggers `ModuleInitializer.OnImport` →
`CirrusServiceProvider.Initialize(PowerShell)`) + DI + extension load — the real PS startup cost.

**Deferred (not in this iteration): core-only baseline.** A second, earlier exit hook
(`CIRRUS_BENCH_INIT_ONLY`) that stops after DI init + dispatch parse but *before* loading any
extension would let us compute the **marginal** cost of each extension
(`marginal(ext) = startup(ext) − baseline`). Useful for *diagnosing* whether a future regression
is in the core or in a specific extension. Deferred to keep this iteration focused; revisit if/when
diagnosis is needed.

---

## 5. Product change — the resolve-only seam

A surface-agnostic environment switch, read once inside `ExecutionSession`, mirroring the existing
`CIRRUS_NO_EXTENSION_CACHE` / `CIRRUS_AGENT_DISABLED` / `--upload-telemetry` hooks:

```
CIRRUS_BENCH_RESOLVE_ONLY=1
```

Behavior — resolve the processor, then return **without executing**:

- `RunAsync()` (CLI/async path):
  ```csharp
  var processor = await GetProcessor();
  if (_resolveOnly) return;          // stop at the seam
  await processor.ProcessAsync();
  ```
- `ExecuteProcessorAsync()` (PS/sync-over-async path): same check after `GetProcessor()`. The
  existing `finally` still calls `OutputChannel.Writer.Complete()`, so the channel pump in `Run()`
  exits cleanly and the cmdlet returns normally.

`_resolveOnly` is read from the environment variable once (e.g. in the `ExecutionSession`
constructor or a static initializer). Exit code is `0`, with no output and no network. This is the
**only** change outside `tests/`, and it carries an XML-doc comment next to the other benchmark
hooks explaining it is a test/benchmark seam.

---

## 6. Benchmark code structure

```
tests/Benchmark/
  Startup/
    StartupBenchmarkBase.cs    # resolve azv2 path + staged .psd1 paths; build ProcessStartInfo
                               # with CIRRUS_BENCH_RESOLVE_ONLY=1; OS page-cache evictor (+capability probe)
    CliStartupBenchmarks.cs    # [Benchmark] Cli_Config_Startup / Cli_Login_Startup
    PsStartupBenchmarks.cs     # [Benchmark] Ps_Config_Startup  / Ps_Login_Startup
  Program.cs                   # add "STARTUP" to --product filter + GetBenchmarkTypes(...)
```

Each surface has a **warm** and a **cold** benchmark class (or a warm/cold pair of methods using
distinct BDN jobs):

- **Warm**: `[SimpleJob(RunStrategy.Monitoring)]`, `InvocationCount=1`, normal warmup → caches hot.
- **Cold**: `[SimpleJob(RunStrategy.ColdStart)]` + `[IterationSetup]` that evicts the OS page cache
  before each measured launch.

Each `[Benchmark]` body: construct `ProcessStartInfo` from the base, `Process.Start`,
`WaitForExit()`, assert `ExitCode == 0`.

### Cold page-cache eviction

- **Linux (CI, `ubuntu-latest`)**: `sudo sh -c 'sync; echo 3 > /proc/sys/vm/drop_caches'`
  (passwordless sudo is available on GitHub-hosted runners).
- **Windows (local dev)**: a Sysinternals-style `EmptyStandbyList.exe standbylist` helper, pinned
  in repo tooling.
- **Capability probe**: if eviction is not permitted on the current agent, the cold path **skips
  with a clear log** (`[Startup] cold eviction unavailable — skipping`) and returns success. We
  never silently report warm numbers as cold.

`RunStrategy.ColdStart` runs each workload as a first-call (no per-iteration warmup), which is the
honest BDN mode for cold-start measurement; combined with per-iteration eviction it yields
genuinely cold launches.

---

## 7. CI integration & output

`.github/workflows/benchmark.yml` gains `--product STARTUP` runs and per-class
`benchmark-action/github-action-benchmark` steps, following the existing CLI/PS pattern (one
`output-file-path` per benchmark class report:
`artifacts/Benchmark/ci/STARTUP/results/Benchmark.Startup.<Class>-report-full-compressed.json`).

| Result | Gate | Dashboard | AppInsights |
|---|---|---|---|
| **Warm** (CLI, PS) | **tracked, not gated** — the 115% threshold flags drift on the dashboard/telemetry only; **no PR comment, no merge block** | `dev/bench/startup-cli`, `dev/bench/startup-ps` | yes |
| **Cold** (CLI, PS) | **informational only** — higher variance; dashboard/telemetry only | same dashboards (separate series) | yes |

- AppInsights export reuses `AppInsightsExporter`, tagging dimensions `Product=STARTUP-CLI` /
  `STARTUP-PS`, `Mode=cold|warm`, `Extension=login|config` so Grafana can slice. `ChartGenerator`
  renders the new rows automatically.
- Startup `github-action-benchmark` steps set `fail-on-alert: false` **and** `comment-on-alert: false`;
  the 115% threshold drives dashboard/telemetry signals only — never a PR comment or merge block (see §10).
- Cold steps run best-effort; an unavailable evictor logs-and-skips and does not fail the job.

---

## 8. Testing & verification

- **Unit (seam):** assert `CIRRUS_BENCH_RESOLVE_ONLY=1` causes `ExecutionSession` to stop after
  `GetProcessor()` and **never** invoke `ProcessAsync()`. Use a stub processor exposing a
  "was-executed" flag; verify it stays false. Covers both the `RunAsync` and `Run`/`ExecuteProcessorAsync` paths.
- **Smoke:** `dotnet run --project tests/Benchmark -c Release -- --local --product STARTUP` runs all
  8 measurements locally and writes artifacts under `artifacts/Benchmark/<timestamp>/`.
- **Manual sanity:** `CIRRUS_BENCH_RESOLVE_ONLY=1 ./artifacts/Release/Core.Cli/azv2 config list`
  exits `0`, emits no output, and makes no network call (confirm via stub/trace).
- **Regression guard:** existing RG benchmarks and `dotnet test` remain green;
  `--product CLI|PS|API` behavior is unchanged.

---

## 9. Footprint summary

- **3 new benchmark files** under `tests/Benchmark/Startup/` (+ `Program.cs` wiring).
- **~6 lines of product code**: one `CIRRUS_BENCH_RESOLVE_ONLY` short-circuit shared by both
  surfaces in `ExecutionSession`.
- **1 unit test** for the seam.
- **Workflow edits** to `benchmark.yml` for STARTUP runs + dashboards.
- Reuses the entire BenchmarkDotNet runner, 115% threshold (dashboard/telemetry signal only),
  gh-pages dashboards, AppInsights export, and chart generation.

## 10. Out of scope (this iteration)

- **PR gating** — no hard merge-block and no per-PR regression comments; startup numbers (especially
  cold) are too noisy, so they are tracked on dashboards + telemetry only.
- In-process (BenchmarkDotNet-host) startup measurement.
- Core-only baseline / marginal-per-extension breakdown (deferred; see §4).
- Phase-instrumented internal stopwatch timings (Approach C).
- ReadyToRun/AOT publishing of the CLI (a potential *fix* the process-level cold benchmark could
  later validate, not part of measuring).
- The `extension` built-in (only `login` and `config` in this iteration).
