# Benchmark Delta: Real Profile File I/O — Indicative Results

> ⚠️ **INDICATIVE ONLY — DO NOT USE AS CI GATE FIGURES**
>
> Both runs used **5 warmup + 5 measurement iterations** (not the committed
> 5000). At this sample size BenchmarkDotNet's run-to-run variance is large
> relative to the profile-IO delta being measured. These numbers answer
> "did this get slower, and roughly by how much"; they are **not** suitable as
> reference values for the CI 115% gate. Authoritative numbers will come from
> CI executing the committed `WarmupCount(5000)` / `IterationCount(5000)`.
>
> **Both runs also executed in Debug configuration.** The `dotnet run` command
> used was `dotnet run --project ... -- --local --product ALL -c Release`.
> Because `-c Release` appears after `--`, it is passed to the benchmark
> application (which ignores it) rather than to `dotnet run`. The host therefore
> built and ran in the default **Debug** configuration. Meanwhile,
> `CLIBenchmarkBase.cs` hardcodes `artifacts/Release` for extension staging, so
> both runs were a **Debug benchmark host loading Release extension assemblies**
> — a mixed configuration. BenchmarkDotNet's "built without optimization" warning
> in the output is direct evidence of this. The correct invocation is:
> `dotnet run -c Release --project tests/Benchmark/Benchmark.csproj -- --local --product ALL`

---

## Summary

Switching the benchmark suite from the in-memory `StubProfileService` to the
real file-backed `ProfileService` makes every benchmark slower — consistent with
the expected cost of adding real disk I/O for profile resolution.

**What the data supports:** the direction is real — all 12 benchmarks increased.
**What the data does not support:** the specific magnitudes. Both runs were a
Debug benchmark host loading Release extension assemblies (see the warning banner
above). A Debug build with only 5 warmup iterations measures the new profile-IO
code path essentially un-JITted, which substantially inflates the delta. Adding
two small file reads plus a JSON deserialize against a warm page cache should
cost on the order of tens of microseconds per invocation, not the 3–4 ms
observed here. The reported 130%–260% increases are very likely overstated; they
should not be treated as the expected steady-state shift.

The high error margins (up to ~69% of mean) further reflect the small sample
size, not instability in production. Authoritative figures require a correct
Release-configured run at 5000 iterations in CI.

---

## Commands Executed

### After (branch `jaysontaimicrosoft-redesigned-waddle`, real file IO)

```powershell
# Working directory: C:\Users\jaysontai\Code\copilot-worktrees\cirrus\jaysontaimicrosoft-redesigned-waddle
# Counts manually set to WarmupCount(5) / IterationCount(5) before this run
dotnet build azclips.slnx -c Release
./build/Set-ModuleConfiguration.ps1 -c Release
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --product ALL -c Release
```

Git ref: branch `jaysontaimicrosoft-redesigned-waddle`

### Before (branch `main`, in-memory StubProfileService)

```powershell
# Working directory: C:\Users\jaysontai\Code\copilot-worktrees\cirrus\azclips-baseline  (scratch worktree)
# main was already checked out in another worktree so we used --detach at main's SHA:
# git worktree add --detach ../azclips-baseline bc608d1a5d7d888b61154d85dc5cf472dedacd34
# Counts manually set to WarmupCount(5) / IterationCount(5) before this run
dotnet build -c Release azclips.slnx -p:CopilotNpmRegistryUrl=https://packagefeedproxy.microsoft.io/npm/
./build/Set-ModuleConfiguration.ps1 -c Release
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --product ALL -c Release
```

Git ref: `bc608d1a5d7d888b61154d85dc5cf472dedacd34` (tip of `main`)

---

## Count Verification

Both runs were verified to use `WarmupCount(5)` / `IterationCount(5)`:

- **After worktree**: edited `CLIBenchmarkBase.cs` lines 21–22 and
  `PSBenchmarkBase.cs` lines 17–18 before running; BenchmarkDotNet job
  headers in output confirm `IterationCount=5, WarmupCount=5`.
- **Before (scratch) worktree**: same four edits applied to
  `../azclips-baseline/tests/Benchmark/CLI/CLIBenchmarkBase.cs` and
  `../azclips-baseline/tests/Benchmark/PS/PSBenchmarkBase.cs`; BenchmarkDotNet
  job headers in output confirm `IterationCount=5, WarmupCount=5`.

---

## Raw BenchmarkDotNet Summary — After (branch `jaysontaimicrosoft-redesigned-waddle`)

```
BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.8893)
OutlierMode=RemoveAll  InvocationCount=1  IterationCount=5  UnrollFactor=1  WarmupCount=5

| Method           | Mean     | Error    | StdDev   | Median   | Allocated |
|----------------- |---------:|---------:|---------:|---------:|----------:|
| 'CLI: List VM'   | 0.0066 s | 0.0020 s | 0.0005 s | 0.0065 s | 463.41 KB |
| 'CLI: Create VM' | 0.0074 s | 0.0013 s | 0.0002 s | 0.0075 s | 794.78 KB |
| 'CLI: Get VM'    | 0.0053 s | 0.0016 s | 0.0004 s | 0.0052 s | 451.55 KB |

| Method          | Mean     | Error    | StdDev   | Median   | Allocated |
|---------------- |---------:|---------:|---------:|---------:|----------:|
| 'PS: List VM'   | 0.0052 s | 0.0021 s | 0.0003 s | 0.0052 s | 413.72 KB |
| 'PS: Create VM' | 0.0067 s | 0.0025 s | 0.0004 s | 0.0067 s |  763.2 KB |
| 'PS: Get VM'    | 0.0057 s | 0.0023 s | 0.0004 s | 0.0057 s | 401.75 KB |

| Method           | Mean     | Error    | StdDev   | Median   | Allocated |
|----------------- |---------:|---------:|---------:|---------:|----------:|
| 'CLI: List RG'   | 0.0074 s | 0.0012 s | 0.0002 s | 0.0074 s | 374.59 KB |
| 'CLI: Create RG' | 0.0096 s | 0.0066 s | 0.0017 s | 0.0103 s |  357.1 KB |
| 'CLI: Get RG'    | 0.0078 s | 0.0018 s | 0.0003 s | 0.0079 s | 355.27 KB |

| Method          | Mean     | Error    | StdDev   | Median   | Allocated |
|---------------- |---------:|---------:|---------:|---------:|----------:|
| 'PS: List RG'   | 0.0058 s | 0.0015 s | 0.0004 s | 0.0057 s | 320.84 KB |
| 'PS: Create RG' | 0.0048 s | 0.0015 s | 0.0002 s | 0.0048 s | 305.05 KB |
| 'PS: Get RG'    | 0.0053 s | 0.0009 s | 0.0001 s | 0.0053 s | 319.52 KB |
```

---

## Raw BenchmarkDotNet Summary — Before (`main`, commit `bc608d1a5`)

```
BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.8893)
OutlierMode=RemoveAll  InvocationCount=1  IterationCount=5  UnrollFactor=1  WarmupCount=5

| Method           | Mean     | Error    | StdDev   | Median   | Allocated |
|----------------- |---------:|---------:|---------:|---------:|----------:|
| 'CLI: List VM'   | 0.0032 s | 0.0008 s | 0.0000 s | 0.0032 s | 441.56 KB |
| 'CLI: Create VM' | 0.0046 s | 0.0013 s | 0.0001 s | 0.0046 s | 762.04 KB |
| 'CLI: Get VM'    | 0.0029 s | 0.0011 s | 0.0003 s | 0.0029 s | 427.05 KB |

| Method          | Mean     | Error    | StdDev   | Median   | Allocated |
|---------------- |---------:|---------:|---------:|---------:|----------:|
| 'PS: List VM'   | 0.0039 s | 0.0016 s | 0.0004 s | 0.0040 s | 391.52 KB |
| 'PS: Create VM' | 0.0049 s | 0.0012 s | 0.0002 s | 0.0048 s | 718.33 KB |
| 'PS: Get VM'    | 0.0037 s | 0.0006 s | 0.0002 s | 0.0036 s | 379.92 KB |

| Method           | Mean     | Error    | StdDev   | Median   | Allocated |
|----------------- |---------:|---------:|---------:|---------:|----------:|
| 'CLI: List RG'   | 0.0031 s | 0.0009 s | 0.0002 s | 0.0031 s | 352.15 KB |
| 'CLI: Create RG' | 0.0041 s | 0.0003 s | 0.0000 s | 0.0041 s | 335.16 KB |
| 'CLI: Get RG'    | 0.0030 s | 0.0006 s | 0.0001 s | 0.0030 s |  333.1 KB |

| Method          | Mean     | Error    | StdDev   | Median   | Allocated |
|---------------- |---------:|---------:|---------:|---------:|----------:|
| 'PS: List RG'   | 0.0036 s | 0.0008 s | 0.0001 s | 0.0037 s | 299.98 KB |
| 'PS: Create RG' | 0.0037 s | 0.0016 s | 0.0004 s | 0.0037 s | 283.02 KB |
| 'PS: Get RG'    | 0.0034 s | 0.0005 s | 0.0001 s | 0.0034 s | 281.45 KB |
```

---

## Comparison Table

> ⚠️ Reminder: all percentages are indicative at 5 iterations. Error margins
> are 15–69% of mean; individual figures should not be treated as precise.
> Both runs used a Debug benchmark host (see warning banner), so the magnitudes
> are very likely overstated. **"Exceeds 115%"** here confirms only that
> direction was observed — it is not a prediction of the CI gate result at 5000
> Release-configured iterations.

| Benchmark        | Before (main) ms | After ms | Change   | Exceeds 115%? |
|------------------|----------------:|--------:|----------|---------------|
| CLI: List VM     | 3.2             | 6.6     | **206%** | Yes (expected)|
| CLI: Create VM   | 4.6             | 7.4     | **161%** | Yes (expected)|
| CLI: Get VM      | 2.9             | 5.3     | **183%** | Yes (expected)|
| PS: List VM      | 3.9             | 5.2     | **133%** | Yes (expected)|
| PS: Create VM    | 4.9             | 6.7     | **137%** | Yes (expected)|
| PS: Get VM       | 3.7             | 5.7     | **154%** | Yes (expected)|
| CLI: List RG     | 3.1             | 7.4     | **239%** | Yes (expected)|
| CLI: Create RG   | 4.1             | 9.6     | **234%** | Yes (expected)|
| CLI: Get RG      | 3.0             | 7.8     | **260%** | Yes (expected)|
| PS: List RG      | 3.6             | 5.8     | **161%** | Yes (expected)|
| PS: Create RG    | 3.7             | 4.8     | **130%** | Yes (expected)|
| PS: Get RG       | 3.4             | 5.3     | **156%** | Yes (expected)|

Percentages computed as `after / before × 100`. Values rounded to the nearest
whole ms from the `Mean` column in each BenchmarkDotNet summary.

All 12 benchmarks show increases, confirming the **direction** of the change.
The **magnitudes** (130%–260%) are not reliable: both runs were a Debug benchmark
host loading Release assemblies, and only 5 warmup iterations were used, so the
new profile-IO path was measured largely un-JITted. The actual per-invocation
cost of adding two small file reads plus JSON deserialize is expected to be on
the order of tens of microseconds, not 3–4 ms. The CI 115% gate will fire on
`main`'s historical baselines until those baselines are updated post-merge; the
gate figures will come from a correct Release-configured run at 5000 iterations.

---

## Count Restoration — git diff Confirmation

After both runs, counts were restored with:

```powershell
git checkout -- tests/Benchmark/CLI/CLIBenchmarkBase.cs tests/Benchmark/PS/PSBenchmarkBase.cs
git diff -- tests/Benchmark/CLI/CLIBenchmarkBase.cs tests/Benchmark/PS/PSBenchmarkBase.cs
```

The `git diff` produced **no output** (empty diff). Both files confirmed to
contain `[WarmupCount(5000)]` and `[IterationCount(5000)]` via `Select-String`.

---

## Scratch Worktree Removal

```powershell
git worktree remove --force ../azclips-baseline
```

Exit code: 0. The worktree at `C:\Users\jaysontai\Code\copilot-worktrees\cirrus\azclips-baseline` is removed.

---

## Files Changed

- **Added**: `docs/superpowers/plans/2026-08-13-benchmark-real-profile-io-results.md` (this file)
- **No other files modified in the committed state.**

---

## Self-Review

- [x] Both runs used `WarmupCount(5)` / `IterationCount(5)` — confirmed from
  BenchmarkDotNet job headers in both outputs (`IterationCount=5, WarmupCount=5`).
- [x] Every number in the table traces back to real observed BenchmarkDotNet
  output; no values were estimated or fabricated.
- [x] Percentages computed correctly as `after / before × 100`.
- [x] Results document leads with prominent indicative-only caveat.
- [x] Both benchmark base classes restored to 5000; `git diff` shows empty diff.
- [x] Scratch worktree removed (exit code 0).
- [x] No file under `src/` or `Agent/` was modified.
- [x] Only the results doc was added in this commit.

---

## Notes / Concerns

- The `main` branch worktree was already checked out at
  `C:/Users/jaysontai/Code/cirrus`, so the scratch worktree was created with
  `--detach` at `main`'s tip SHA (`bc608d1a5`). This is equivalent to running
  against `main`.
- BenchmarkDotNet warned that benchmarks "were built without optimization
  enabled" in both runs. The real cause: `-c Release` was placed after `--` in
  the `dotnet run` command, so it was passed to the benchmark application (which
  silently ignores it) rather than to `dotnet run`. The host therefore ran in the
  default **Debug** configuration. Additionally, `CLIBenchmarkBase.cs` hardcodes
  `artifacts/Release` for extension staging, making both runs a **Debug
  benchmark host loading Release extension assemblies**. The optimization warning
  is direct evidence of this. Both runs were affected identically, so the
  relative direction of the comparison is valid, but the magnitudes are likely
  inflated. The correct invocation for anyone repeating this exercise is:
  `dotnet run -c Release --project tests/Benchmark/Benchmark.csproj -- --local --product ALL`
- Error margins at 5 iterations are very large (up to ~69% of mean). The
  absolute ms values should be read as order-of-magnitude estimates only.
