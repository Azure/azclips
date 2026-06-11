# Cirrus Benchmark Suite

Performance benchmarking for Cirrus CLI/PowerShell engine, comparing execution time across CLI and PowerShell products.

## Quick Start

### Run Benchmarks

```bash
# Run all benchmarks (API, CLI, PS) - uploads to App Insights
dotnet run --project tests/Benchmark/Benchmark.csproj

# Run in local mode (skip App Insights upload)
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local

# Run specific product only
dotnet run --project tests/Benchmark/Benchmark.csproj -- --product CLI
dotnet run --project tests/Benchmark/Benchmark.csproj -- --product PS

# Combine options
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --product CLI
```

### Output

Results are saved to `artifacts/Benchmark/<timestamp>/`:
- `benchmark-comparison-*.html` - Grouped bar chart (Chart.js)
- `benchmark-details-*.html` - Detailed breakdown
- `benchmark-summary-*.json` - JSON data for programmatic use

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
| Max relative error | 1% | `[MaxRelativeError(0.01)]` |
| Max iteration count | 1000 | `[MaxIterationCount(1000)]` |
| Outlier handling | Remove all | `[Outliers(OutlierMode.RemoveAll)]` |
| Memory diagnostics | Enabled (allocations tracked) | `[MemoryDiagnoser]` |

Warmup and measurement iteration counts are determined dynamically by BenchmarkDotNet: it keeps iterating until the measured mean is within the 1% relative error target or until `MaxIterationCount` is hit. Each iteration runs the benchmark method exactly once (cold-start semantics) — `[IterationSetup]` re-registers stub HTTP/auth/telemetry services and (for CLI) extension loaders before every run, and `[IterationCleanup]` disposes the DI container.

### Product Implementations

| Product | Implementation            | Measures                           |
|---------|---------------------------|------------------------------------|
| CLI | Run via Program.cs        | Execution                          |
| PS | In-process Runspace (SDK) | Cmdlet execution in shared session |

---

## Project Structure

```
tests/Benchmark/
├── Program.cs              # Entry point, argument parsing
├── BenchmarkBase.cs        # Shared setup (auth, cleanup, utilities)
├── ChartGenerator.cs       # HTML/JSON chart generation
├── AppInsightsExporter.cs  # Metrics upload to App Insights
├── CLI/
│   └── ResourceGroupBenchmarks.cs   # CLI process benchmarks
└── PS/
    └── ResourceGroupBenchmarks.cs   # PowerShell cmdlet benchmarks
```

---

## Command Line Options

| Option | Description | Example |
|--------|-------------|---------|
| `--local` | Skip App Insights upload | `-- --local` |
| `--product <name>` | Run specific product only (API/CLI/PS/ALL) | `-- --product CLI` |

---

## Current Test Scenarios

Azure Resource Group operations:
- **List** - `GET /subscriptions/{id}/resourcegroups`
- **Create** - `PUT /subscriptions/{id}/resourcegroups/{name}`
- **Get** - `GET /subscriptions/{id}/resourcegroups/{name}`

---

## Telemetry & Feedback Loop

```
Benchmark Runner ──▶ App Insights ──▶ Grafana ──▶ Analysis ──▶ New Test Cases
       ▲                                                              │
       └──────────────────────────────────────────────────────────────┘
```

Running benchmarks uploads two types of data:
1. **Benchmark Metrics** - Aggregated latency (Mean, StdDev) per operation
2. **Command Telemetry** - Fine-grained traces from CLI/PS execution

Analyze telemetry to discover bottlenecks → Add new benchmark cases → Validate fixes.

---

## See Also

- [Grafana Dashboard](https://ms.portal.azure.com#view/Microsoft_Azure_PinToGrafana/AzureGrafana.ReactView/GalleryType/azure%20monitor/ConfigurationId/%2Fsubscriptions%2F9e223dbe-3399-4e19-88eb-0975f02ac87f%2FresourceGroups%2Fnori-app-insights%2Fproviders%2FMicrosoft.Dashboard%2Fdashboards%2Fnori-grafana-0203) - Live performance dashboard
