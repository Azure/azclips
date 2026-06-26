// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using Tests.Common.Utilities;

namespace Benchmark;

public class Program
{
    public static int Main(string[] args)
    {
        // Parse arguments
        var productFilter = ParseProductFilter(args);
        var localMode = ParseLocalMode(args);
        var ciMode = ParseCiMode(args);

        if (ciMode)
        {
            localMode = true;
            Console.WriteLine("[Benchmark] CI mode: --local is implied and outputs use a stable path");
        }

        if (localMode)
        {
            Console.WriteLine("[Benchmark] Local mode: App Insights upload will be skipped");
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        var artifactsPath = ciMode
            ? Path.Combine(RepoLocator.GetRepoRoot(), "artifacts", "Benchmark", "ci", productFilter ?? "ALL")
            : Path.Combine(RepoLocator.GetRepoRoot(), "artifacts", "Benchmark", timestamp);

        var config = ManualConfig.CreateEmpty()
            .AddColumnProvider(DefaultColumnProviders.Instance)
            .AddColumn(StatisticColumn.Mean)
            .AddColumn(StatisticColumn.StdDev)
            .AddColumn(StatisticColumn.Median)
            .AddExporter(JsonExporter.FullCompressed)
            .AddExporter(MarkdownExporter.GitHub)
            .AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default)
            .AddAnalyser(BenchmarkDotNet.Analysers.EnvironmentAnalyser.Default)
            .WithArtifactsPath(artifactsPath)
            .WithSummaryStyle(SummaryStyle.Default
                .WithMaxParameterColumnWidth(50)
                .WithTimeUnit(TimeUnit.Second))
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        // Get benchmark types based on --product filter
        var benchmarkTypes = GetBenchmarkTypes(productFilter);
        Console.WriteLine($"[Benchmark] Running {benchmarkTypes.Length} benchmark class(es): {string.Join(", ", benchmarkTypes.Select(t => t.Name))}");

        // Run all matching benchmarks directly (no interactive menu).
        // .ToArray() materializes the runs so every class executes even if an
        // earlier one fails — failures are aggregated post-hoc into the exit code.
        var summaries = benchmarkTypes.Select(t => BenchmarkRunner.Run(t, config)).ToArray();

        var failures = new List<string>();
        foreach (var s in summaries)
        {
            failures.AddRange(InspectSummary(s));
        }

        // Generate charts (local artifacts) for whatever did succeed — gives the
        // human at the terminal the full picture even on a partial failure.
        var chartData = ChartGenerator.Generate(artifactsPath, timestamp, summaries);

        // Export to App Insights for historical data (skip in local mode)
        if (!localMode)
        {
            if (!ExportToAppInsights(chartData, timestamp))
            {
                failures.Add("[AppInsights] export failed");
            }
        }
        else
        {
            Console.WriteLine("[AppInsights] Skipped (local mode)");
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("\n[Benchmark] PASS — all benchmarks completed successfully.");
            return 0;
        }

        Console.WriteLine($"\n[Benchmark] FAIL — {failures.Count} failure(s):");
        foreach (var f in failures)
        {
            Console.WriteLine($"  - {f}");
        }
        return 1;
    }

    /// <summary>
    /// Inspect a single BenchmarkDotNet <see cref="Summary"/> for failure signals
    /// and return a list of human-readable failure descriptions.
    /// </summary>
    /// <remarks>
    /// BenchmarkDotNet swallows exceptions thrown inside [Benchmark], [GlobalSetup],
    /// and [GlobalCleanup] methods. They surface only via <see cref="Summary"/> state:
    /// failed reports, missing reports, or critical validation errors. This helper
    /// translates that state into a flat list the runner can use to set a non-zero
    /// exit code.
    /// </remarks>
    private static List<string> InspectSummary(Summary summary)
    {
        var failures = new List<string>();

        // Prefer a discovered case for the type name; fall back to the run title
        // (e.g. when validation prevented case discovery from completing).
        var typeName = summary.BenchmarksCases.FirstOrDefault()?.Descriptor.Type.Name
            ?? summary.Title
            ?? "<unknown>";

        if (summary.HasCriticalValidationErrors)
        {
            failures.Add($"[Validation] {typeName}: critical validation errors");
            foreach (var err in summary.ValidationErrors.Where(v => v.IsCritical))
            {
                failures.Add($"    {err.Message}");
            }
        }
        else if (summary.Reports.Length == 0)
        {
            // BDN emits an empty report set when [GlobalSetup] throws or no
            // case could run for some other non-validation reason.
            failures.Add($"[Setup] {typeName}: no reports produced (likely GlobalSetup failure)");
        }

        foreach (var report in summary.Reports.Where(r => !r.Success))
        {
            // ConsoleLogger.Default already prints the stack trace separately;
            // this entry just ensures the failure is named in the final verdict.
            failures.Add($"[Benchmark] {typeName}.{report.BenchmarkCase.Descriptor.WorkloadMethodDisplayInfo}: failed");
        }

        return failures;
    }

    /// <summary>
    /// Export benchmark metrics to Application Insights.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the export completed without error; <c>false</c> when an
    /// exception was caught. The friendly error message is still printed either way.
    /// </returns>
    private static bool ExportToAppInsights(List<ChartDataPoint> chartData, string timestamp)
    {
        try
        {
            using var exporter = new AppInsightsExporter();
            exporter.Export(chartData, timestamp);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppInsights] Export failed: {ex.Message}");
            return false;
        }
    }

    private static Type[] GetBenchmarkTypes(string? product)
    {
        var allTypes = new[]
        {
            typeof(CLI.ResourceGroupBenchmarks),
            typeof(PS.ResourceGroupBenchmarks)
        };

        if (string.IsNullOrEmpty(product) || product == "ALL")
        {
            return allTypes;
        }

        return product switch
        {
            "CLI" => new[] { typeof(CLI.ResourceGroupBenchmarks) },
            "PS" => new[] { typeof(PS.ResourceGroupBenchmarks) },
            "STARTUP" => GetStartupBenchmarkTypes(),
            _ => throw new ArgumentException($"Unknown product: {product}. Use: API, CLI, PS, STARTUP, or ALL")
        };
    }

    /// <summary>
    /// Startup benchmark classes. Warm always runs; cold runs only when OS page-cache eviction
    /// is available on this machine (otherwise it is skipped with a log — never reported as
    /// cold-but-actually-warm).
    /// </summary>
    private static Type[] GetStartupBenchmarkTypes()
    {
        var types = new List<Type>
        {
            typeof(Startup.CliWarmStartupBenchmarks),
            typeof(Startup.PsWarmStartupBenchmarks),
        };

        if (Startup.StartupEviction.IsAvailable())
        {
            types.Add(typeof(Startup.CliColdStartupBenchmarks));
            types.Add(typeof(Startup.PsColdStartupBenchmarks));
        }
        else
        {
            Console.WriteLine("[Startup] cold eviction unavailable — skipping cold startup benchmarks");
        }

        return types.ToArray();
    }

    private static string? ParseProductFilter(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--product" && i + 1 < args.Length)
            {
                return args[i + 1].ToUpperInvariant();
            }
            if (args[i].StartsWith("--product="))
            {
                return args[i].Substring("--product=".Length).ToUpperInvariant();
            }
        }
        return null;
    }

    private static bool ParseLocalMode(string[] args)
    {
        return args.Any(arg => arg == "--local" || arg == "-l");
    }

    private static bool ParseCiMode(string[] args)
    {
        return args.Any(arg => arg == "--ci");
    }
}
