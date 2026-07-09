# TEMP PR Plan: CLI Cold Start Investigation

## Goal

Investigate and reduce slow first-invocation startup time for `az` (CLI path), while preserving normal behavior.

## Problem Summary

Observed behavior:

- First invocation after rebuild or long idle appears slow.
- Immediately following invocations are fast.

Initial hypothesis:

- OS page cache / RAM eviction effects may be a significant factor.

## Cursory Findings (Code Walk)

Likely cold-path contributors observed:

- `src/CLI/Program.cs`
    - Builds DI container every process start (`AzclipsServiceProvider.Initialize`).
    - Resolves config, telemetry service, dispatcher, engine on startup path.
    - Spawns telemetry uploader process in `finally`.
- `src/CLI/Telemetry/CLITelemetryProvider.cs`
    - Builds OpenTelemetry `TracerProvider` when telemetry is enabled.
    - Adds HTTP instrumentation and exporter configuration at startup.
- `src/CLI/AssemblyLoading/ExtensionLoader.cs`
    - Built-in and multi-version loaders do filesystem probing and assembly load-by-stream.
    - Probing roots and candidate names can incur disk I/O on cold starts.
- `src/Core/Core.Configuration/Implementation/ConfigurationService.cs`
    - Reads JSON config and environment overrides for option resolution; startup can include file I/O.

Potentially useful but currently not wired into CLI bootstrap:

- `src/Core/Core.Telemetry/TimingActivityListener.cs` supports `AZCLIPS_TIMING` activity timing output, but no install call is currently found in startup path.

## Scope

In scope:

- Measure startup latency differences (cold vs warm, Debug vs Release).
- Identify top startup cost buckets (host/CLR, DI/config, telemetry init, extension probing/load).
- Produce data-backed recommendation(s) and low-risk improvements.

Out of scope (for this temporary branch plan):

- Large architecture changes.
- Behavior changes not related to startup performance.

## Measurement Matrix

Run each scenario with repeated samples and report p50/p95:

- Build mode:
    - [ ] Debug
    - [ ] Release
- Invocation state:
    - [ ] Cold after build
    - [ ] Warm immediate rerun
    - [ ] Cold-ish after long idle (for example 10-30 min)
- Telemetry:
    - [ ] Telemetry enabled
    - [ ] Telemetry disabled (`az config set telemetry.enabled false`)
- Extension/cache behavior:
    - [ ] Default
    - [ ] `--no-cache`
- Command shape:
    - [ ] Lightweight intrinsic (`az --version`)
    - [ ] Help (`az help`)
    - [ ] Typical extension-backed command (pick one representative)

## Quick Benchmark Starter (PowerShell)

Use this as a repeatable baseline loop for one scenario at a time.

```powershell
$samples = 10
$results = @()
for ($i = 1; $i -le $samples; $i++) {
    $ms = (Measure-Command { az --version *> $null }).TotalMilliseconds
    $results += [math]::Round($ms, 2)
}

$sorted = $results | Sort-Object
$p50 = $sorted[[int]([math]::Floor(($samples - 1) * 0.50))]
$p95 = $sorted[[int]([math]::Floor(($samples - 1) * 0.95))]

"Samples(ms): $($results -join ', ')"
"p50(ms): $p50"
"p95(ms): $p95"
```

Notes:

- Keep command and environment stable between scenario runs.
- Redirect command output during timing to reduce console rendering noise.
- Run separate loops for Debug vs Release artifacts.

## Investigation Checklist

### 1) Baseline and Repro

- [ ] Define exact command set for benchmarking and keep it fixed.
- [ ] Record environment: OS version, CPU, disk type, power mode.
- [ ] Capture at least 10 samples per scenario (more if noisy).
- [ ] Report p50/p95 and standard deviation.

### 2) Isolate Debug vs Release

- [ ] Compare Debug vs Release binaries with same command set.
- [ ] Confirm whether slow-cold behavior exists in Release similarly.
- [ ] If only Debug is impacted heavily, quantify ratio and decide if product impact is real.

### 3) Determine Cold-Start Cost Buckets

- [ ] Measure host startup only (`az --version`) vs extension command to split host vs extension load costs.
- [ ] Compare telemetry on/off to estimate telemetry init overhead.
- [ ] Compare default vs `--no-cache` to estimate extension cache/probing impact.
- [ ] Validate whether first-run file system probing in extension loaders dominates.

### 4) RAM/Page Cache Hypothesis Validation

- [ ] Perform warm run and confirm low latency.
- [ ] Introduce idle period and re-measure.
- [ ] Reboot or memory-pressure scenario (if practical) and re-measure.
- [ ] Distinguish process startup/CLR JIT effects from pure disk cache effects.

### 5) Instrumentation Plan (Short-Lived)

- [ ] Add temporary stopwatch markers in `Program.Main` for key phases:
    - DI initialize
    - config resolution
    - telemetry provider init
    - parse/dispatch
    - engine session open
- [ ] Add temporary logging around extension loader probing/load path.
- [ ] Keep instrumentation behind env var flag to avoid noisy default output.
- [ ] Remove or convert to guarded diagnostics before merge.

### 6) Candidate Optimizations (Only After Data)

- [ ] Defer expensive telemetry initialization until first real command activity.
- [ ] Reduce extension probing breadth or memoize resolved paths for process lifetime.
- [ ] Avoid unnecessary service resolution for intrinsic cold-path commands (`--version`, help where possible).
- [ ] Evaluate whether assembly load-by-stream is needed everywhere or can be narrowed.

### 7) Validation Gates

- [ ] Functional regression check for representative commands.
- [ ] Ensure telemetry behavior remains correct when enabled/disabled.
- [ ] Confirm no regressions for extension discovery/load correctness.

## Data Capture Template

Use this table for each benchmark command.

| Scenario | Command      | Build   | Telemetry | Cache mode | Sample count | p50 ms | p95 ms | Notes |
| -------- | ------------ | ------- | --------- | ---------- | -----------: | -----: | -----: | ----- |
| Example  | az --version | Release | On        | Default    |           10 |        |        |       |

## Exit Criteria

- [ ] We can explain the cold vs warm delta with measured evidence.
- [ ] We have ranked contributors with rough cost estimates.
- [ ] We have a prioritized fix list (small, medium, larger) with risk notes.
- [ ] We have clear recommendation on whether to ship changes now or continue profiling.
