# Azclips Benchmark Suite

Performance benchmarking for Azclips CLI/PowerShell engine, comparing execution time across CLI and PowerShell products.

## Quick Start

### Prerequisites

Benchmarks execute the **Release** build artifacts (CLI host, extensions, and PowerShell module DLLs), so build in Release first:

```bash
# Build the solution in Release (produces artifacts/Release/...)
dotnet build azclips.slnx -c Release

# Stage PS module manifests into artifacts/Release/PS/ (required for PS benchmarks)
./build/Set-ModuleConfiguration.ps1 -c Release
```

### Run Benchmarks

```bash
# Run all benchmarks (CLI + PS) - uploads results to App Insights
dotnet run --project tests/Benchmark/Benchmark.csproj

# Run in local mode (skip App Insights upload)
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local

# Run a specific product only (CLI or PS)
dotnet run --project tests/Benchmark/Benchmark.csproj -- --product CLI
dotnet run --project tests/Benchmark/Benchmark.csproj -- --product PS

# Combine options
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --product CLI

# CI mode (implies --local, writes to a stable artifacts path)
dotnet run --project tests/Benchmark/Benchmark.csproj -- --ci --product CLI
```

> **Charts — run all products for the comparison.** The local grouped-bar chart
> (`benchmark-comparison-*.html`) plots CLI vs PowerShell side by side, so populate
> both series by running all products (the default):
>
> ```bash
> # Both series populated (recommended for the comparison chart):
> dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --product ALL
>
> # Scope to one extension while keeping the CLI-vs-PS comparison:
> dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --product ALL --extension Compute
> ```
>
> Running a single product (`--product CLI` or `--product PS`) leaves the other series
> as zero-height bars — handy for a quick single-product check, not for comparison.

### Output

Results are saved to `artifacts/Benchmark/<timestamp>/` (or `artifacts/Benchmark/ci/<PRODUCT>/` in `--ci` mode):
- `benchmark-comparison-*.html` - Grouped bar chart (Chart.js)
- `benchmark-details-*.html` - Detailed breakdown
- `benchmark-summary-*.json` - JSON data for programmatic use
- `results/` - BenchmarkDotNet's own exports (full JSON + GitHub-flavored Markdown)
- `telemetry/<PRODUCT>.<Class>.<Method>.jsonl` - 100 sampled CLI traces per benchmark method (one span per line)

When `--sidecar-out` is passed (CI does this; a plain local run does not), aggregated span trees are written as `<sidecar-out>/<EXT|LOCAL>/<PRODUCT>/<Benchmark>.json` and uploaded as the `benchmark-trace-sidecars` workflow artifact.

---

## Architecture

### Technology Stack

- **[BenchmarkDotNet](https://benchmarkdotnet.org/)** - .NET micro-benchmarking framework
- **Application Insights** - Metrics export for Grafana dashboards

### Test Configuration

Configured via BenchmarkDotNet attributes on `CLIBenchmarkBase` / `PSBenchmarkBase`:

| Setting | Value | Attribute |
|---------|-------|-----------|
| Invocations per iteration | 1 (unroll factor 1) | `[InvocationCount(1, 1)]` |
| Warmup iterations | 5,000 | `[WarmupCount(5000)]` |
| Measurement iterations | 5,000 | `[IterationCount(5000)]` |
| Outlier handling | Remove all | `[Outliers(OutlierMode.RemoveAll)]` |
| Memory diagnostics | Enabled (allocations tracked) | `[MemoryDiagnoser]` |

Each iteration runs the benchmark method exactly once (`[InvocationCount(1, 1)]`); 5,000 warmup and 5,000 measurement iterations are collected per benchmark. The two products are configured to measure the same thing — the warm, in-process per-command pipeline — excluding the cost of standing up their respective execution environments (process launch is excluded by running in-process; extension/module loading is excluded from the measured region on both sides). Per-iteration setup keeps every run isolated:

- `[GlobalSetup]` (once per class): discovers the repo root; for CLI it also stages the required extensions into a benchmark-scoped `artifacts/Benchmark/home/extensions` layout; for PS it stages the module manifest closure under `artifacts/Benchmark/PS`.
- `[IterationSetup]` (before each run): re-registers stub HTTP/auth services; for CLI it also re-registers the extension loaders + registry store pointed at the staged home, and swaps telemetry back to the production `TelemetryService` with the exporter aimed at this iteration's warmup/measured directory; for PS it creates and opens a fresh runspace and imports the module (both excluded from the measurement, so only the cmdlet invocation is measured).
- `[IterationCleanup]` (after each run): disposes the DI container (which clears all registrations); for PS it also closes and disposes the runspace created in setup.
- `[GlobalCleanup]` (once per class): removes the staged extensions home (best-effort); for CLI it also samples the measured traces down to 100 and deletes the raw trace files.

### Product Implementations

| Product | Implementation | Measures |
|---------|----------------|----------|
| CLI | In-process `Core.Cli.Program.Main(args)` | Warm CLI command pipeline (extension loaded once into a process-wide ALC) |
| PS | Cmdlet invocation on a runspace + module imported in `[IterationSetup]` (PowerShell SDK) | Warm cmdlet execution (runspace creation + module import excluded) |

HTTP is short-circuited by a resource-aware stub terminate handler (`StubHttpTerminateHandler`, backed by `StubResponseMappings` + `Stubs/Fixtures/*.json`): it returns a minimal, correctly-shaped ARM body per resource type (or `{}` for unmapped paths) plus a `Retry-After: 0` header, so benchmarks measure the local CLI/PS pipeline only, never call Azure, and orchestrated long-running creates complete without real polling.

---

## Project Structure

```
tests/Benchmark/
├── Program.cs                  # Entry point: arg parsing, BDN config, exit-code aggregation
├── BenchmarkBase.cs            # Shared setup: repo root, stub DI registration, cleanup
├── BenchmarkEventListener.cs   # BenchmarkEventPublisher: no-op IEventPublisher stub
├── BenchmarkExtensionAttribute.cs  # [BenchmarkExtension("Name")] discovery marker
├── ChartGenerator.cs           # HTML/JSON chart generation
├── AppInsightsExporter.cs      # Benchmark metrics upload to App Insights
├── CLI/
│   ├── CLIBenchmarkBase.cs                       # BDN attributes, extension staging + DI overrides
│   ├── BenchmarkAssemblyConfigurationService.cs  # Points extension loaders at the staged home
│   ├── ResourceGroupBenchmarks.cs                # CLI: List / Create / Get RG  [Resources]
│   ├── UserAssignedIdentityBenchmarks.cs         # CLI: List / Create / Get / Update UAI [ManagedServiceIdentity]
│   └── VirtualMachineBenchmarks.cs               # CLI: List / Create / Get VM  [Compute]
└── PS/
    ├── PSBenchmarkBase.cs                         # BDN attributes, runspace + module-import helper
    ├── ResourceGroupBenchmarks.cs                # PS: List / Create / Get RG   [Resources]
    ├── UserAssignedIdentityBenchmarks.cs         # PS: List / Create / Get / Update UAI [ManagedServiceIdentity]
    └── VirtualMachineBenchmarks.cs               # PS: List / Create / Get VM   [Compute]
```

---

## Command Line Options

| Option | Description | Example |
|--------|-------------|---------|
| `--local`, `-l` | Skip App Insights upload | `-- --local` |
| `--product <name>` | Run a specific product only (`CLI`, `PS`, or `ALL`) | `-- --product CLI` |
| `--ci` | CI mode: implies `--local` and writes to a stable `artifacts/Benchmark/ci/<EXTENSION>/<PRODUCT>/` path | `-- --ci --product CLI` |
| `--extension <name>` | Run a specific extension only (for example `Compute`, `ManagedServiceIdentity`, `Resources`, or `ALL`) | `-- --extension Compute` |
| `--list` | Print the discovered benchmark classes (extension / product / name) and exit | `-- --list` |

---

## Current Test Scenarios

Azure Resource Group operations, run against stubbed HTTP (no real Azure calls):

| Operation | CLI command | PowerShell cmdlet | Conceptual REST call |
|-----------|-------------|-------------------|----------------------|
| List | `group list` | `Get-AzResourceGroup` | `GET /subscriptions/{id}/resourcegroups` |
| Create | `group create` | `New-AzResourceGroup` | `PUT /subscriptions/{id}/resourcegroups/{name}` |
| Get | `group show` | `Get-AzResourceGroup -Name` | `GET /subscriptions/{id}/resourcegroups/{name}` |

### Compute (Virtual Machine)

Azure Virtual Machine operations, run against the resource-aware stubbed HTTP (no real Azure calls):

| Operation | CLI command | PowerShell cmdlet | Notes |
|-----------|-------------|-------------------|-------|
| List | `vm list` | `Get-AzVM` (by resource group) | GET, non-LRO |
| Create | `vm create` | `New-AzVM` | Orchestrated: VNet -> NSG -> NIC -> VM (LRO) |
| Get | `vm show` | `Get-AzVM -Name` | GET, non-LRO |

Create is a long-running operation; the benchmark stub returns `Retry-After: 0` and a
minimally-shaped virtual-network body so the orchestrated create completes without
real polling. See "Project Structure" and `tests/Common/Stubs/`.

### ManagedServiceIdentity (User-Assigned Managed Identity)

Azure user-assigned managed identity operations, run against the resource-aware stubbed HTTP (no real Azure calls):

| Operation | CLI command | PowerShell cmdlet | Notes |
|-----------|-------------|-------------------|-------|
| List | `identity list` | `Get-AzUserAssignedIdentity` (by resource group) | GET, non-LRO |
| Create | `identity create` | `New-AzUserAssignedIdentity` | PUT, includes tags, isolation scope, and resource restrictions |
| Get | `identity show` | `Get-AzUserAssignedIdentity -Name` | GET, non-LRO |
| Update | `identity update` | `Update-AzUserAssignedIdentity` | PATCH, updates tags and isolation scope |

---

## Telemetry & Feedback Loop

```
Benchmark Runner ──▶ App Insights ──▶ Grafana ──▶ Analysis ──▶ New Test Cases
       ▲                                                              │
       └──────────────────────────────────────────────────────────────┘
```

Unless `--local`/`--ci` is set, each run uploads aggregated benchmark metrics to App Insights via `AppInsightsExporter`:
1. **`BenchmarkDuration`** custom metric - mean duration per result
2. **`BenchmarkResult`** custom event - duration (s/ms) plus `Product`, `Operation`, `Resource`, and `Timestamp` dimensions for Grafana filtering

Aggregated benchmark results are what get exported to App Insights. Command-level telemetry is stubbed out for PS benchmarks (`StubTelemetryService`), but CLI benchmarks emit real spans to `artifacts/Benchmark/<timestamp>/telemetry/` so a slow benchmark can be broken down by phase locally. Those files are never uploaded. Analyze them to spot bottlenecks → add new benchmark cases → validate fixes.

---

## Comparing traces against main

CI aggregates each benchmark's sampled spans into one averaged span tree and publishes
`main`'s trees under `dev/bench/traces/<EXT>/<PRODUCT>/<Benchmark>.json`. A
pull-request run reads them back and renders `main` and the PR as two trees side by side
in the workflow **job summary**, with a per-span `Δ%` between them. The comparison lives
only in the job summary — a block is wide enough to need horizontal scrolling in a PR
comment, so no trace comment is posted. Open the workflow run to read it.

**Bars are not comparable across the gutter.** Each side's `%ROOT` and bar are scaled to
that side's own root, exactly as a standalone tree is. When the roots differ, an
unchanged span draws a different bar on each side. Compare using the `AVG` and `Δ%`
columns; read each bar as a within-tree proportion only.

The bar is `#` characters, one per completed 5% of the root (20 cells), truncated rather
than rounded — so 24% draws four cells, 26% draws five, and anything under 5% draws none
and is read from its `%ROOT` number. The cell count is derived from the *printed*
percentage, so `cells = %ROOT × 20 ÷ 100` always holds and the bar can be read off the
number beside it. It uses ASCII rather than block elements (`█`, `▏`…) because those are
Unicode East Asian Width *Ambiguous*: a browser that font-falls-back on them renders them
at a width that is not the monospace cell, and since a row's block count varies from 0 to
20, the columns after the bar drift and the two trees stop lining up.

A span present on only one side renders a blank row on the absent side and `n/a` in
`Δ%` — it was added, removed, or renamed rather than made slower, and any real cost
change still shows in its parent's `Δ%`.

Baselines refresh only from the main pushes the path filter selects, so each extension's
baseline sits at its own commit. The header states which: `main @ abc1234 · 13 days ago`.
A shared change (`src/Core`, `tests/Common`, the benchmark base classes) re-runs every
extension and realigns them all.

To compare two local runs without CI:

```bash
git switch main
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --extension Resources
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input artifacts/Benchmark/<timestamp> --sidecar-out ./main-traces

git switch my-branch
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --extension Resources
python3 .github/scripts/aggregate-benchmark-traces.py \
  --input artifacts/Benchmark/<timestamp> --baseline ./main-traces
```

Local runs write telemetry under `artifacts/Benchmark/<timestamp>/telemetry/` with no extension segment (just `<PRODUCT>.<Class>.<Method>.jsonl`). When aggregating, both the `--sidecar-out` and `--baseline` paths use `LOCAL/<PRODUCT>/<Benchmark>.json`, so a `./main-traces` baseline from one local run matches the sidecar path computed from another. The product is extracted from the stem's first segment, so CLI and PS benchmarks land in separate directories.

Omitting `--baseline` renders the single-tree view.

---

## Adding a New Extension

1. Add `CLI/<Resource>Benchmarks.cs` and `PS/<Resource>Benchmarks.cs`, each tagged
   `[BenchmarkExtension("<Name>")]`. Use a single, title-cased `<Name>` (e.g. `Compute`,
   `Accounts`) — it becomes the dashboard title (`Azclips <Name> CLI Benchmarks`).
   `RequiredExtensions` (CLI) and `ModuleName` (PS) are derived from the attribute, so
   there is nothing else to wire up on the class.
2. Keep the product source under `src/Extensions/<Name>/` (and/or `src/PS/<Name>/`) —
   the standard folder convention CI uses to detect which extension changed.
3. If the extension makes orchestrated/LRO calls that read response fields, add the
   minimal shape to `tests/Common/Stubs/StubResponseMappings.cs` + a fixture file.

That's it — **no workflow or `Program.cs` edits are required.** The extension universe,
the CI run matrix, per-extension change detection, the publish dashboards, and the
fork-PR comment are all derived at runtime from the `[BenchmarkExtension]` attribute
(see `.github/scripts/detect-benchmark-changes.sh`). Changes to shared code
(`src/Core`, benchmark base classes, the workflow, the detection script, …) re-run
every extension; a change confined to one extension's source or benchmark class runs
only that extension.

---

## See Also

- [Grafana Dashboard](https://ms.portal.azure.com#view/Microsoft_Azure_PinToGrafana/AzureGrafana.ReactView/GalleryType/azure%20monitor/ConfigurationId/%2Fsubscriptions%2F9e223dbe-3399-4e19-88eb-0975f02ac87f%2FresourceGroups%2Fnori-app-insights%2Fproviders%2FMicrosoft.Dashboard%2Fdashboards%2Fnori-grafana-0203) - Live performance dashboard
