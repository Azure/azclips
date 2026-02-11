// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;

namespace Benchmark;

public class Program
{
    public static void Main(string[] args)
    {
        // Parse --product argument
        var productFilter = ParseProductFilter(args);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var artifactsPath = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "Benchmark", timestamp);

        var config = ManualConfig.CreateEmpty()
            .AddColumnProvider(DefaultColumnProviders.Instance)
            .AddColumn(StatisticColumn.Mean)
            .AddColumn(StatisticColumn.StdDev)
            .AddColumn(StatisticColumn.Median)
            .AddExporter(JsonExporter.Brief)
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

        // Run all matching benchmarks directly (no interactive menu)
        var summaries = benchmarkTypes.Select(t => BenchmarkRunner.Run(t, config)).ToArray();

        // Generate charts
        ChartGenerator.Generate(artifactsPath, timestamp, summaries);
    }

    private static Type[] GetBenchmarkTypes(string? product)
    {
        var allTypes = new[]
        {
            typeof(API.ResourceGroupBenchmarks),
            typeof(CLI.ResourceGroupBenchmarks),
            typeof(PS.ResourceGroupBenchmarks)
        };

        if (string.IsNullOrEmpty(product) || product == "ALL")
        {
            return allTypes;
        }

        return product switch
        {
            "API" => new[] { typeof(API.ResourceGroupBenchmarks) },
            "CLI" => new[] { typeof(CLI.ResourceGroupBenchmarks) },
            "PS" => new[] { typeof(PS.ResourceGroupBenchmarks) },
            _ => throw new ArgumentException($"Unknown product: {product}. Use: API, CLI, PS, or ALL")
        };
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
}
