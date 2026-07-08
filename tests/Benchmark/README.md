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

HTTP is short-circuited by a stub terminate handler (returns `{}` 200 OK), so benchmarks measure the local CLI/PS pipeline only and never call Azure.

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
| `--ci` | CI mode: implies `--local` and writes to a stable `artifacts/Benchmark/ci/<PRODUCT>/` path | `-- --ci --product CLI` |
| `--extension <name>` | Run a specific extension only (`Compute`, `Resources`, or `ALL`) | `-- --extension Compute` |
| `--list` | Print the discovered benchmark classes (extension / product / name) and exit | `-- --list` |

---

## Current Test Scenarios

Azure Resource Group operations, run against stubbed HTTP (no real Azure calls):

| Operation | CLI command | PowerShell cmdlet | Conceptual REST call |
|-----------|-------------|-------------------|----------------------|
| List | `group list` | `Get-ResourceGroup` | `GET /subscriptions/{id}/resourcegroups` |
| Create | `group create` | `New-ResourceGroup` | `PUT /subscriptions/{id}/resourcegroups/{name}` |
| Get | `group show` | `Get-ResourceGroup -Name` | `GET /subscriptions/{id}/resourcegroups/{name}` |

### Compute (Virtual Machine)

Azure Virtual Machine operations, run against the resource-aware stubbed HTTP (no real Azure calls):

| Operation | CLI command | PowerShell cmdlet | Notes |
|-----------|-------------|-------------------|-------|
| List | `vm list` | `Get-VM` (by resource group) | GET, non-LRO |
| Create | `vm create` | `New-VM` | Orchestrated: VNet -> NSG -> NIC -> VM (LRO) |
| Get | `vm show` | `Get-VM -Name` | GET, non-LRO |

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
   `[BenchmarkExtension("<Name>")]` and overriding `RequiredExtensions` / `ModuleName`.
2. If the extension makes orchestrated/LRO calls that read response fields, add the
   minimal shape to `tests/Common/Stubs/StubResponseMappings.cs` + a fixture file.
3. Add a `<name>:` filter block (and its files) in `.github/workflows/benchmark.yml`.

Discovery (reflection), the CI matrix, the report glob, and the comment workflow pick
it up automatically — no `Program.cs` or matrix-list edits required.

---

## See Also

- [Grafana Dashboard](https://ms.portal.azure.com#view/Microsoft_Azure_PinToGrafana/AzureGrafana.ReactView/GalleryType/azure%20monitor/ConfigurationId/%2Fsubscriptions%2F9e223dbe-3399-4e19-88eb-0975f02ac87f%2FresourceGroups%2Fnori-app-insights%2Fproviders%2FMicrosoft.Dashboard%2Fdashboards%2Fnori-grafana-0203) - Live performance dashboard
