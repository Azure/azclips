// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Benchmark.Telemetry;
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
        var ciMode = ParseCiMode(args);
        var extensionFilter = ParseExtensionFilter(args);
        var listMode = ParseListMode(args);

        if (ciMode)
        {
            Console.WriteLine("[Benchmark] CI mode: outputs use a stable path");
        }

        if (ParseLocalMode(args))
        {
            Console.WriteLine("[Benchmark] --local/-l is accepted for compatibility; results are always local artifacts");
        }

        if (listMode)
        {
            var discovered = GetBenchmarkTypes(productFilter, extensionFilter);
            Console.WriteLine($"[Benchmark] Discovered {discovered.Length} class(es):");
            foreach (var t in discovered)
            {
                var ext = t.GetCustomAttribute<BenchmarkExtensionAttribute>()?.Name;
                Console.WriteLine(ext is null
                    ? $"  - Startup / {t.Name}"
                    : $"  - {ext} / {ProductOf(t)} / {t.Name}");
            }
            return 0;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        var artifactsPath = ciMode
            ? Path.Combine(RepoLocator.GetRepoRoot(), "artifacts", "Benchmark", "ci", extensionFilter ?? "ALL", productFilter ?? "ALL")
            : Path.Combine(RepoLocator.GetRepoRoot(), "artifacts", "Benchmark", timestamp);

        // Benchmarks write telemetry to a stable staging path because their child processes cannot
        // know artifactsPath. Clear it first so post-run collection can only pick up this run.
        var telemetryStagingRoot = BenchmarkTelemetryArtifacts.StagingRoot(RepoLocator.GetRepoRoot());
        DirectoryUtils.TryDeleteDirectory(telemetryStagingRoot);

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
        var benchmarkTypes = GetBenchmarkTypes(productFilter, extensionFilter);
        if (benchmarkTypes.Length == 0)
        {
            throw new InvalidOperationException(
                $"No benchmark classes found for extension='{extensionFilter ?? "ALL"}', product='{productFilter ?? "ALL"}'.");
        }

        Console.WriteLine($"[Benchmark] Running {benchmarkTypes.Length} benchmark class(es): {string.Join(", ", benchmarkTypes.Select(t => t.Name))}");

        // Run all matching benchmarks directly (no interactive menu).
        // .ToArray() materializes the runs so every class executes even if an
        // earlier one fails — failures are aggregated post-hoc into the exit code.
        var summaries = benchmarkTypes.Select(t => BenchmarkRunner.Run(t, config)).ToArray();

        CollectTelemetry(telemetryStagingRoot, Path.Combine(artifactsPath, "telemetry"));

        var failures = new List<string>();
        foreach (var s in summaries)
        {
            failures.AddRange(InspectSummary(s));
        }

        // Generate charts (local artifacts) for whatever did succeed — gives the
        // human at the terminal the full picture even on a partial failure.
        ChartGenerator.Generate(artifactsPath, timestamp, summaries);

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
    /// Discovers benchmark classes by reflection and filters them by product (inferred
    /// from the base type) and extension (from <see cref="BenchmarkExtensionAttribute"/>).
    /// A class missing the attribute is a hard error — this keeps the taxonomy honest.
    /// </summary>
    private static Type[] GetBenchmarkTypes(string? product, string? extension)
    {
        // Startup benchmarks are a separate family (process-level, own base class, no
        // [BenchmarkExtension]); select them only via an explicit --product STARTUP. They are
        // never part of reflection-based extension discovery or the ALL set.
        if (string.Equals(product, "STARTUP", StringComparison.OrdinalIgnoreCase))
        {
            return GetStartupBenchmarkTypes();
        }

        var allTypes = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(CLI.CLIBenchmarkBase).IsAssignableFrom(t)
                     || typeof(PS.PSBenchmarkBase).IsAssignableFrom(t))
            .ToArray();

        var untagged = allTypes
            .Where(t => t.GetCustomAttribute<BenchmarkExtensionAttribute>() is null)
            .Select(t => t.Name)
            .ToArray();
        if (untagged.Length > 0)
        {
            throw new InvalidOperationException(
                $"Benchmark classes missing [BenchmarkExtension]: {string.Join(", ", untagged)}");
        }

        IEnumerable<Type> filtered = allTypes;

        if (!string.IsNullOrEmpty(product) && product != "ALL")
        {
            filtered = product switch
            {
                "CLI" => filtered.Where(t => typeof(CLI.CLIBenchmarkBase).IsAssignableFrom(t)),
                "PS" => filtered.Where(t => typeof(PS.PSBenchmarkBase).IsAssignableFrom(t)),
                _ => throw new ArgumentException($"Unknown product: {product}. Use: CLI, PS, STARTUP, or ALL")
            };
        }

        if (!string.IsNullOrEmpty(extension) && extension != "ALL")
        {
            filtered = filtered.Where(t => string.Equals(
                t.GetCustomAttribute<BenchmarkExtensionAttribute>()!.Name,
                extension,
                StringComparison.OrdinalIgnoreCase));
        }

        return filtered
            .OrderBy(t => t.GetCustomAttribute<BenchmarkExtensionAttribute>()!.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => ProductOf(t), StringComparer.Ordinal)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ProductOf(Type t) =>
        typeof(CLI.CLIBenchmarkBase).IsAssignableFrom(t) ? "CLI" : "PS";

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
            Console.WriteLine(
                $"[Startup] cold eviction unavailable ({Startup.StartupEviction.DescribeUnavailable()}) — skipping cold startup benchmarks");
        }

        return types.ToArray();
    }

    /// <summary>
    /// Moves the pruned per-benchmark telemetry files into this run's artifacts folder.
    /// </summary>
    /// <remarks>Never throws and never contributes to the failure list.</remarks>
    private static void CollectTelemetry(string stagingRoot, string destination)
    {
        try
        {
            var files = Directory.Exists(stagingRoot)
                ? Directory.GetFiles(stagingRoot, "*.jsonl")
                : Array.Empty<string>();

            if (files.Length == 0)
            {
                Console.WriteLine("[Telemetry] No telemetry was produced by this run");
                return;
            }

            Directory.CreateDirectory(destination);
            foreach (var file in files)
            {
                File.Move(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
            }

            Console.WriteLine($"[Telemetry] Collected {files.Length} file(s) into {destination}");
            DirectoryUtils.TryDeleteDirectory(stagingRoot);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Telemetry] Collection failed, benchmarks unaffected: {ex.Message}");
        }
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

    private static string? ParseExtensionFilter(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--extension" && i + 1 < args.Length)
            {
                return args[i + 1].ToUpperInvariant();
            }
            if (args[i].StartsWith("--extension="))
            {
                return args[i].Substring("--extension=".Length).ToUpperInvariant();
            }
        }
        return null;
    }

    private static bool ParseListMode(string[] args)
    {
        return args.Any(arg => arg == "--list");
    }
}
