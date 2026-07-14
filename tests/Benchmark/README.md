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
| Measurement iterations | 10,000 (fixed) | `[IterationCount(10000)]` |
| Outlier handling | Remove all | `[Outliers(OutlierMode.RemoveAll)]` |
| Memory diagnostics | Enabled (allocations tracked) | `[MemoryDiagnoser]` |

Each iteration runs the benchmark method exactly once (`[InvocationCount(1, 1)]`), giving cold-start semantics, and a fixed 10,000 measurement iterations are collected per benchmark (`[IterationCount(10000)]`). Per-iteration setup keeps every run isolated:

- `[GlobalSetup]` (once per class): discovers the repo root; for CLI it also stages the required extensions into a benchmark-scoped `artifacts/Benchmark/home/extensions` layout.
- `[IterationSetup]` (before each run): re-registers stub HTTP/auth/telemetry services, and for CLI re-registers the extension loaders + registry store pointed at the staged home.
- `[IterationCleanup]` (after each run): disposes the DI container (which clears all registrations).
- `[GlobalCleanup]` (once per class): removes the staged extensions home (best-effort).

### Product Implementations

| Product | Implementation | Measures |
|---------|----------------|----------|
| CLI | In-process `Core.Cli.Program.Main(args)` | Full CLI command pipeline, cold start per call |
| PS | Fresh runspace + `Import-Module` per invocation (PowerShell SDK) | Cmdlet execution, cold start per call |

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
│   └── VirtualMachineBenchmarks.cs               # CLI: List / Create / Get VM  [Compute]
└── PS/
    ├── PSBenchmarkBase.cs                         # BDN attributes, runspace + module-import helper
    ├── ResourceGroupBenchmarks.cs                # PS: List / Create / Get RG   [Resources]
    └── VirtualMachineBenchmarks.cs               # PS: List / Create / Get VM   [Compute]
```

---

## Command Line Options

| Option | Description | Example |
|--------|-------------|---------|
| `--local`, `-l` | Skip App Insights upload | `-- --local` |
| `--product <name>` | Run a specific product only (`CLI`, `PS`, or `ALL`) | `-- --product CLI` |
| `--ci` | CI mode: implies `--local` and writes to a stable `artifacts/Benchmark/ci/<EXTENSION>/<PRODUCT>/` path | `-- --ci --product CLI` |
| `--extension <name>` | Run a specific extension only (`Compute`, `Resources`, or `ALL`) | `-- --extension Compute` |
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

Command-level telemetry is stubbed out during benchmarks (`StubTelemetryService`) to keep measurements isolated, so only these aggregated results are exported. Analyze them to spot bottlenecks → add new benchmark cases → validate fixes.

---

## Adding a New Extension

1. Add `CLI/<Resource>Benchmarks.cs` and `PS/<Resource>Benchmarks.cs`, each tagged
   `[BenchmarkExtension("<Name>")]`. Use a single, title-cased `<Name>` (e.g. `Compute`,
   `Login`) — it becomes the dashboard title (`Azclips <Name> CLI Benchmarks`).
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
