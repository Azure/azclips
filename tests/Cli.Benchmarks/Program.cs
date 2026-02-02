using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;

namespace Cli.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var artifactsPath = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "Benchmark", timestamp);

        var config = ManualConfig.CreateEmpty()
            .AddColumnProvider(DefaultColumnProviders.Instance)
            .AddColumn(StatisticColumn.Mean)
            .AddColumn(StatisticColumn.StdDev)
            .AddColumn(StatisticColumn.Median)
            .AddExporter(JsonExporter.Brief)  // Brief doesn't generate compressed file
            .AddExporter(MarkdownExporter.GitHub)
            .AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default)
            .AddAnalyser(BenchmarkDotNet.Analysers.EnvironmentAnalyser.Default)
            .WithArtifactsPath(artifactsPath)
            .WithSummaryStyle(SummaryStyle.Default
                .WithMaxParameterColumnWidth(50)
                .WithTimeUnit(TimeUnit.Second))
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        // Run all benchmarks in the assembly, or filter by args
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);

        // Generate bar chart after benchmarks complete
        GenerateBarChart(artifactsPath, timestamp, summaries);
    }

    private static void GenerateBarChart(string artifactsPath, string timestamp, IEnumerable<Summary> summaries)
    {
        var chartData = new List<(string Method, double MeanSeconds)>();

        foreach (var summary in summaries)
        {
            if (summary?.Reports == null) continue;

            foreach (var report in summary.Reports)
            {
                if (report?.ResultStatistics == null) continue;

                var methodName = report.BenchmarkCase.Descriptor.WorkloadMethodDisplayInfo;
                var meanNs = report.ResultStatistics.Mean;
                var meanSeconds = meanNs / 1_000_000_000.0; // ns to seconds

                chartData.Add((methodName, meanSeconds));
            }
        }

        if (chartData.Count == 0) return;

        // Calculate overhead (CLI time - Pipeline time)
        var overheadData = CalculateOverhead(chartData);

        // Generate HTML bar chart with overhead comparison
        var htmlPath = Path.Combine(artifactsPath, $"benchmark-chart-{timestamp}.html");
        var html = GenerateHtmlChart(chartData, overheadData, timestamp);
        File.WriteAllText(htmlPath, html);
        Console.WriteLine($"\n[Chart] Generated: {htmlPath}");

        // Also generate JSON summary
        var jsonPath = Path.Combine(artifactsPath, $"benchmark-summary-{timestamp}.json");
        var jsonData = new
        {
            Timestamp = timestamp,
            Results = chartData.Select(x => new { Method = x.Method, MeanSeconds = Math.Round(x.MeanSeconds, 4) }),
            Overhead = overheadData.Select(x => new { Operation = x.Operation, CliTime = Math.Round(x.CliTime, 4), PipelineTime = Math.Round(x.PipelineTime, 4), Overhead = Math.Round(x.Overhead, 4) })
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[Chart] Summary JSON: {jsonPath}");
    }

    private static List<(string Operation, double CliTime, double PipelineTime, double Overhead)> CalculateOverhead(List<(string Method, double MeanSeconds)> data)
    {
        var overheadList = new List<(string Operation, double CliTime, double PipelineTime, double Overhead)>();

        // Map CLI commands to their corresponding Pipeline benchmarks
        var mappings = new Dictionary<string, string>
        {
            { "CLI: group list", "Pipeline: List RG" },
            { "CLI: group create", "Pipeline: Create RG" },
            { "CLI: group show", "Pipeline: Get RG" }
        };

        foreach (var (cliName, pipelineName) in mappings)
        {
            var cliResult = data.FirstOrDefault(x => x.Method == cliName);
            var pipelineResult = data.FirstOrDefault(x => x.Method == pipelineName);

            if (cliResult.Method != null && pipelineResult.Method != null)
            {
                var operation = cliName.Replace("CLI: group ", "").ToUpperInvariant();
                var overhead = cliResult.MeanSeconds - pipelineResult.MeanSeconds;
                overheadList.Add((operation, cliResult.MeanSeconds, pipelineResult.MeanSeconds, overhead));
            }
        }

        return overheadList;
    }

    private static string GenerateHtmlChart(
        List<(string Method, double MeanSeconds)> data,
        List<(string Operation, double CliTime, double PipelineTime, double Overhead)> overheadData,
        string timestamp)
    {
        var maxValue = data.Max(x => x.MeanSeconds);
        
        // Chart 1: All benchmarks mean time
        var bars = string.Join("\n", data.Select(x =>
        {
            var widthPercent = (x.MeanSeconds / maxValue) * 100;
            var barClass = x.Method.StartsWith("CLI") ? "bar-cli" : "bar-pipeline";
            return $@"
        <div class=""bar-container"">
            <div class=""bar-label"">{x.Method}</div>
            <div class=""bar-wrapper"">
                <div class=""bar {barClass}"" style=""width: {widthPercent:F1}%""></div>
                <span class=""bar-value"">{x.MeanSeconds:F4}s</span>
            </div>
        </div>";
        }));

        // Chart 2: Overhead comparison (stacked bar)
        var overheadBars = "";
        if (overheadData.Count > 0)
        {
            var maxCliTime = overheadData.Max(x => x.CliTime);
            overheadBars = string.Join("\n", overheadData.Select(x =>
            {
                var pipelineWidthPercent = (x.PipelineTime / maxCliTime) * 100;
                var overheadWidthPercent = (x.Overhead / maxCliTime) * 100;
                var overheadPercent = (x.Overhead / x.CliTime) * 100;
                return $@"
        <div class=""overhead-container"">
            <div class=""overhead-label"">{x.Operation}</div>
            <div class=""stacked-bar-wrapper"">
                <div class=""stacked-bar"">
                    <div class=""stacked-segment pipeline"" style=""width: {pipelineWidthPercent:F1}%"" title=""Pipeline: {x.PipelineTime:F4}s""></div>
                    <div class=""stacked-segment overhead"" style=""width: {overheadWidthPercent:F1}%"" title=""Overhead: {x.Overhead:F4}s""></div>
                </div>
                <span class=""overhead-value"">CLI: {x.CliTime:F4}s | Overhead: {x.Overhead:F4}s ({overheadPercent:F1}%)</span>
            </div>
        </div>";
            }));
        }

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <title>Benchmark Results - {timestamp}</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 40px; background: #f5f5f5; }}
        h1 {{ color: #333; margin-bottom: 30px; }}
        h2 {{ color: #555; margin-top: 30px; margin-bottom: 20px; }}
        .chart {{ background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); margin-bottom: 30px; }}
        .bar-container, .overhead-container {{ margin: 15px 0; }}
        .bar-label, .overhead-label {{ font-weight: 500; margin-bottom: 5px; color: #555; }}
        .bar-wrapper, .stacked-bar-wrapper {{ display: flex; align-items: center; }}
        .bar {{ height: 30px; border-radius: 4px; min-width: 2px; }}
        .bar-cli {{ background: linear-gradient(90deg, #2196F3, #64B5F6); }}
        .bar-pipeline {{ background: linear-gradient(90deg, #4CAF50, #8BC34A); }}
        .bar-value, .overhead-value {{ margin-left: 10px; font-weight: bold; color: #333; white-space: nowrap; }}
        .stacked-bar {{ display: flex; height: 30px; border-radius: 4px; overflow: hidden; flex-grow: 1; max-width: 60%; }}
        .stacked-segment {{ height: 100%; }}
        .stacked-segment.pipeline {{ background: #4CAF50; }}
        .stacked-segment.overhead {{ background: #FF9800; }}
        .legend {{ display: flex; gap: 20px; margin-top: 20px; flex-wrap: wrap; }}
        .legend-item {{ display: flex; align-items: center; gap: 5px; }}
        .legend-color {{ width: 20px; height: 20px; border-radius: 3px; }}
        .timestamp {{ color: #999; font-size: 12px; margin-top: 20px; }}
        .summary-table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
        .summary-table th, .summary-table td {{ padding: 10px; text-align: left; border-bottom: 1px solid #eee; }}
        .summary-table th {{ background: #f9f9f9; font-weight: 600; }}
    </style>
</head>
<body>
    <h1>📊 Benchmark Results</h1>
    
    <div class=""chart"">
        <h2>Mean Execution Time (seconds)</h2>
        {bars}
        <div class=""legend"">
            <div class=""legend-item""><div class=""legend-color"" style=""background: #4CAF50;""></div> Pipeline (HTTP Request)</div>
            <div class=""legend-item""><div class=""legend-color"" style=""background: #2196F3;""></div> CLI (Full Command)</div>
        </div>
    </div>

    <div class=""chart"">
        <h2>CLI Overhead Analysis</h2>
        <p style=""color: #666; margin-bottom: 20px;"">Shows the breakdown of CLI command time: Pipeline (API) time vs. CLI Overhead (startup, auth, parsing, etc.)</p>
        {overheadBars}
        <div class=""legend"">
            <div class=""legend-item""><div class=""legend-color"" style=""background: #4CAF50;""></div> Pipeline Time (API Request)</div>
            <div class=""legend-item""><div class=""legend-color"" style=""background: #FF9800;""></div> CLI Overhead</div>
        </div>

        {(overheadData.Count > 0 ? $@"
        <table class=""summary-table"">
            <tr><th>Operation</th><th>CLI Total</th><th>Pipeline (API)</th><th>Overhead</th><th>Overhead %</th></tr>
            {string.Join("\n", overheadData.Select(x => $@"
            <tr>
                <td><strong>{x.Operation}</strong></td>
                <td>{x.CliTime:F4}s</td>
                <td>{x.PipelineTime:F4}s</td>
                <td>{x.Overhead:F4}s</td>
                <td>{(x.Overhead / x.CliTime * 100):F1}%</td>
            </tr>"))}
        </table>" : "")}
    </div>

    <p class=""timestamp"">Generated: {timestamp}</p>
</body>
</html>";
    }
}
