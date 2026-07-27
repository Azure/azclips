// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using BenchmarkDotNet.Reports;

namespace Benchmark;

/// <summary>
/// Generates HTML charts and JSON summaries from benchmark results.
/// </summary>
public static class ChartGenerator
{
    /// <summary>
    /// Generate all chart outputs from benchmark summaries.
    /// </summary>
    /// <param name="artifactsPath">Path to store artifacts.</param>
    /// <param name="timestamp">Timestamp for file naming.</param>
    /// <param name="summaries">Benchmark summaries to process.</param>
    /// <returns>Extracted chart data points for further processing.</returns>
    public static List<ChartDataPoint> Generate(string artifactsPath, string timestamp, IEnumerable<Summary> summaries)
    {
        var chartData = ExtractChartData(summaries);
        if (chartData.Count == 0) return chartData;

        var timings = CalculateOverhead(chartData);

        // Generate professional grouped bar chart (Chart.js)
        var chartJsPath = Path.Combine(artifactsPath, $"benchmark-comparison-{timestamp}.html");
        File.WriteAllText(chartJsPath, GenerateGroupedBarChart(timings, timestamp));
        Console.WriteLine($"\n[Chart] Generated: {chartJsPath}");

        // Generate detailed HTML chart
        var htmlPath = Path.Combine(artifactsPath, $"benchmark-details-{timestamp}.html");
        File.WriteAllText(htmlPath, GenerateDetailsChart(chartData, timings, timestamp));
        Console.WriteLine($"[Chart] Details: {htmlPath}");

        // Generate JSON summary
        var jsonPath = Path.Combine(artifactsPath, $"benchmark-summary-{timestamp}.json");
        var jsonData = new
        {
            Timestamp = timestamp,
            Results = chartData.Select(x => new { x.Method, x.Product, x.Mode, x.Extension, MeanSeconds = Math.Round(x.MeanSeconds, 4) }),
            Operations = timings.Select(x => new { x.Operation, x.CliTime, x.PsTime })
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[Chart] Summary JSON: {jsonPath}");

        return chartData;
    }

    private static List<ChartDataPoint> ExtractChartData(IEnumerable<Summary> summaries)
    {
        var chartData = new List<ChartDataPoint>();

        foreach (var summary in summaries)
        {
            if (summary?.Reports == null) continue;

            foreach (var report in summary.Reports)
            {
                if (report?.ResultStatistics == null) continue;

                var methodName = report.BenchmarkCase.Descriptor.WorkloadMethodDisplayInfo;
                var fullName = report.BenchmarkCase.Descriptor.Type.FullName ?? "";
                var meanSeconds = report.ResultStatistics.Mean / 1_000_000_000.0;

                string product;
                string mode = "";
                string extension = "";
                if (fullName.Contains(".Startup."))
                {
                    product = fullName.Contains("Ps") ? "STARTUP-PS" : "STARTUP-CLI";
                    mode = fullName.Contains("Cold") ? "cold" : "warm";
                    extension = methodName.ToLowerInvariant(); // [Benchmark(Description=...)] = config/login/extension
                }
                else
                {
                    product = fullName.Contains(".CLI.") ? "CLI" :
                              fullName.Contains(".PS.") ? "PS" : "Other";
                }

                chartData.Add(new ChartDataPoint(methodName, product, meanSeconds, mode, extension));
            }
        }

        return chartData;
    }

    private static List<OverheadData> CalculateOverhead(List<ChartDataPoint> data)
    {
        // Derive the operation label from each benchmark's display name by stripping the
        // leading product prefix ("CLI: " / "PS: "). This keeps the comparison view
        // extension-agnostic — whatever operations ran show up automatically, in
        // first-seen order — instead of a hardcoded Resource-Group list.
        return data
            .Select(x => OperationLabel(x.Method))
            .Distinct()
            .Select(op =>
            {
                var cliResult = data.FirstOrDefault(x => OperationLabel(x.Method) == op && x.Product == "CLI");
                var psResult = data.FirstOrDefault(x => OperationLabel(x.Method) == op && x.Product == "PS");

                return new OverheadData
                {
                    Operation = op,
                    CliTime = Math.Round(cliResult?.MeanSeconds ?? 0, 4),
                    PsTime = Math.Round(psResult?.MeanSeconds ?? 0, 4)
                };
            }).ToList();
    }

    // "CLI: List VM" -> "List VM"; "PS: Create RG" -> "Create RG"; falls back to the
    // whole trimmed name when there is no "<product>: " prefix. BenchmarkDotNet wraps
    // the display name in single quotes (e.g. 'CLI: List VM'), so strip those first.
    private static string OperationLabel(string method)
    {
        var label = method.Trim().Trim('\'');
        var colon = label.IndexOf(':');
        return colon >= 0 ? label[(colon + 1)..].Trim() : label.Trim();
    }

    //TODO: Refactor chart generation
    private static string GenerateGroupedBarChart(List<OverheadData> timings, string timestamp)
    {
        var operations = timings.Select(x => $"'{x.Operation}'").ToArray();
        var cliData = timings.Select(x => x.CliTime.ToString("F4")).ToArray();
        var psData = timings.Select(x => x.PsTime.ToString("F4")).ToArray();

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <title>Benchmark Comparison - {timestamp}</title>
    <script src=""https://cdn.jsdelivr.net/npm/chart.js""></script>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            padding: 40px;
            background: #f8f9fa;
            margin: 0;
        }}
        .container {{
            max-width: 1200px;
            margin: 0 auto;
        }}
        h1 {{
            color: #1a1a2e;
            margin-bottom: 10px;
            font-size: 28px;
        }}
        .subtitle {{
            color: #666;
            margin-bottom: 30px;
            font-size: 14px;
        }}
        .chart-container {{
            background: white;
            border-radius: 12px;
            padding: 30px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.07);
            margin-bottom: 30px;
        }}
        .chart-wrapper {{
            position: relative;
            height: 400px;
        }}
        .stats-grid {{
            display: grid;
            grid-template-columns: repeat(3, 1fr);
            gap: 20px;
            margin-top: 30px;
        }}
        .stat-card {{
            background: white;
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.07);
        }}
        .stat-card h3 {{
            margin: 0 0 15px 0;
            color: #333;
            font-size: 16px;
            border-bottom: 2px solid #eee;
            padding-bottom: 10px;
        }}
        .stat-row {{
            display: flex;
            justify-content: space-between;
            padding: 8px 0;
            border-bottom: 1px solid #f0f0f0;
        }}
        .stat-row:last-child {{
            border-bottom: none;
        }}
        .stat-label {{
            color: #666;
            font-size: 13px;
        }}
        .stat-value {{
            font-weight: 600;
            font-size: 13px;
        }}
        .stat-value.cli {{ color: #3b82f6; }}
        .stat-value.ps {{ color: #8b5cf6; }}
        .timestamp {{
            text-align: center;
            color: #999;
            font-size: 12px;
            margin-top: 20px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>📊 Benchmark Comparison</h1>
        <p class=""subtitle"">Execution time comparison across CLI and PowerShell</p>

        <div class=""chart-container"">
            <div class=""chart-wrapper"">
                <canvas id=""comparisonChart""></canvas>
            </div>
        </div>

        <div class=""stats-grid"">
            {string.Join("\n", timings.Select(x => $@"
            <div class=""stat-card"">
                <h3>{x.Operation}</h3>
                <div class=""stat-row"">
                    <span class=""stat-label"">CLI (az.exe)</span>
                    <span class=""stat-value cli"">{x.CliTime:F4}s</span>
                </div>
                <div class=""stat-row"">
                    <span class=""stat-label"">PS (pwsh.exe)</span>
                    <span class=""stat-value ps"">{x.PsTime:F4}s</span>
                </div>
            </div>"))}
        </div>

        <p class=""timestamp"">Generated: {timestamp}</p>
    </div>

    <script>
        const ctx = document.getElementById('comparisonChart').getContext('2d');
        new Chart(ctx, {{
            type: 'bar',
            data: {{
                labels: [{string.Join(", ", operations)}],
                datasets: [
                    {{
                        label: 'CLI (az.exe)',
                        data: [{string.Join(", ", cliData)}],
                        backgroundColor: 'rgba(59, 130, 246, 0.8)',
                        borderColor: 'rgba(59, 130, 246, 1)',
                        borderWidth: 1,
                        borderRadius: 4
                    }},
                    {{
                        label: 'PS (pwsh.exe)',
                        data: [{string.Join(", ", psData)}],
                        backgroundColor: 'rgba(139, 92, 246, 0.8)',
                        borderColor: 'rgba(139, 92, 246, 1)',
                        borderWidth: 1,
                        borderRadius: 4
                    }}
                ]
            }},
            options: {{
                responsive: true,
                maintainAspectRatio: false,
                plugins: {{
                    legend: {{
                        position: 'top',
                        labels: {{
                            usePointStyle: true,
                            padding: 20,
                            font: {{ size: 13 }}
                        }}
                    }},
                    title: {{
                        display: false
                    }},
                    tooltip: {{
                        callbacks: {{
                            label: function(context) {{
                                return context.dataset.label + ': ' + context.raw.toFixed(4) + 's';
                            }}
                        }}
                    }}
                }},
                scales: {{
                    x: {{
                        grid: {{
                            display: false
                        }},
                        ticks: {{
                            font: {{ size: 13, weight: '500' }}
                        }}
                    }},
                    y: {{
                        beginAtZero: true,
                        title: {{
                            display: true,
                            text: 'Execution Time (seconds)',
                            font: {{ size: 13 }}
                        }},
                        grid: {{
                            color: 'rgba(0,0,0,0.05)'
                        }},
                        ticks: {{
                            callback: function(value) {{
                                return value.toFixed(1) + 's';
                            }}
                        }}
                    }}
                }},
                barPercentage: 0.8,
                categoryPercentage: 0.7
            }}
        }});
    </script>
</body>
</html>";
    }

    private static string GenerateDetailsChart(
        List<ChartDataPoint> data,
        List<OverheadData> timings,
        string timestamp)
    {
        var maxValue = data.Max(x => x.MeanSeconds);

        var productSections = GenerateProductSections(data, maxValue);
        var startupComparison = GenerateStartupComparison(data);

        // Comparison table (RG operations across CLI/PS). Hidden when empty, e.g. a startup-only run.
        var comparisonRows = string.Join("\n", timings.Where(x => x.CliTime > 0 || x.PsTime > 0).Select(x => $@"
            <tr>
                <td><strong>{x.Operation}</strong></td>
                <td>{x.CliTime:F4}s</td>
                <td>{x.PsTime:F4}s</td>
            </tr>"));

        var crossProductSection = string.IsNullOrWhiteSpace(comparisonRows) ? "" : $@"
    <div class=""chart"">
        <h2>Cross-Product Comparison</h2>
        <p style=""color: #666; margin-bottom: 20px;"">Side-by-side execution time for each operation across CLI and PowerShell.</p>
        <table class=""comparison-table"">
            <tr>
                <th>Operation</th>
                <th>CLI</th>
                <th>PS</th>
            </tr>
            {comparisonRows}
        </table>
    </div>";

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <title>Benchmark Details - {timestamp}</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 40px; background: #f5f5f5; }}
        h1 {{ color: #333; margin-bottom: 30px; }}
        h2, h3 {{ color: #555; margin-top: 20px; }}
        .chart {{ background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); margin-bottom: 30px; }}
        .product-section {{ margin-bottom: 30px; padding-bottom: 20px; border-bottom: 1px solid #eee; }}
        .bar-container {{ margin: 10px 0; }}
        .bar-label {{ font-weight: 500; margin-bottom: 5px; color: #555; font-size: 14px; }}
        .bar-wrapper {{ display: flex; align-items: center; }}
        .bar {{ height: 25px; border-radius: 4px; min-width: 2px; }}
        .bar-cli {{ background: linear-gradient(90deg, #2196F3, #64B5F6); }}
        .bar-ps {{ background: linear-gradient(90deg, #9C27B0, #BA68C8); }}
        .bar-startup-cli {{ background: linear-gradient(90deg, #FF9800, #FFB74D); }}
        .bar-startup-cli-cold {{ background: linear-gradient(90deg, #E65100, #FB8C00); }}
        .bar-startup-ps {{ background: linear-gradient(90deg, #009688, #4DB6AC); }}
        .bar-startup-ps-cold {{ background: linear-gradient(90deg, #00695C, #00897B); }}
        .bar-other {{ background: linear-gradient(90deg, #757575, #BDBDBD); }}
        .bar-value {{ margin-left: 10px; font-weight: bold; color: #333; white-space: nowrap; }}
        .legend {{ display: flex; gap: 20px; margin-top: 20px; flex-wrap: wrap; }}
        .legend-item {{ display: flex; align-items: center; gap: 5px; }}
        .legend-color {{ width: 20px; height: 20px; border-radius: 3px; }}
        .comparison-table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
        .comparison-table th, .comparison-table td {{ padding: 12px; text-align: left; border-bottom: 1px solid #eee; }}
        .comparison-table th {{ background: #f9f9f9; font-weight: 600; }}
        .delta-pos {{ color: #c0392b; font-weight: 600; }}
        .delta-neg {{ color: #1e8449; font-weight: 600; }}
        .timestamp {{ color: #999; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <h1>📊 Benchmark Details</h1>

    <div class=""chart"">
        <h2>Execution Time by Product</h2>
        {productSections}
        <div class=""legend"">
            <div class=""legend-item""><div class=""legend-color"" style=""background: #2196F3;""></div> CLI (az.exe)</div>
            <div class=""legend-item""><div class=""legend-color"" style=""background: #9C27B0;""></div> PS (pwsh.exe)</div>
            <div class=""legend-item""><div class=""legend-color"" style=""background: #FF9800;""></div> Startup CLI — Warm</div>
            <div class=""legend-item""><div class=""legend-color"" style=""background: #E65100;""></div> Startup CLI — Cold</div>
            <div class=""legend-item""><div class=""legend-color"" style=""background: #009688;""></div> Startup PS — Warm</div>
            <div class=""legend-item""><div class=""legend-color"" style=""background: #00695C;""></div> Startup PS — Cold</div>
        </div>
    </div>
{startupComparison}
{crossProductSection}

    <p class=""timestamp"">Generated: {timestamp}</p>
</body>
</html>";
    }

    /// <summary>Preferred display order for startup operations so warm and cold sections line up row-for-row.</summary>
    private static readonly string[] ExtensionOrder = { "config", "login", "extension" };

    private static int ExtOrder(string method)
    {
        var idx = Array.IndexOf(ExtensionOrder, method.ToLowerInvariant());
        return idx < 0 ? int.MaxValue : idx;
    }

    /// <summary>
    /// Render the "Execution Time by Product" bar sections. Startup benchmarks are split by run mode so
    /// warm and cold never share a section — each lands under its own labelled heading.
    /// </summary>
    private static string GenerateProductSections(List<ChartDataPoint> data, double maxValue)
    {
        // Group key includes Mode so STARTUP-CLI warm and STARTUP-CLI cold become distinct sections.
        var groups = data
            .GroupBy(x => (x.Product, x.Mode))
            .OrderBy(g => SectionSort(g.Key.Product, g.Key.Mode).product)
            .ThenBy(g => SectionSort(g.Key.Product, g.Key.Mode).mode);

        return string.Join("\n", groups.Select(g =>
        {
            var bars = string.Join("\n", g
                .OrderBy(x => ExtOrder(x.Method))
                .ThenBy(x => x.Method)
                .Select(x =>
                {
                    var widthPercent = maxValue > 0 ? (x.MeanSeconds / maxValue) * 100 : 0;
                    var barClass = BarClass(x);
                    return $@"
            <div class=""bar-container"">
                <div class=""bar-label"">{x.Method}</div>
                <div class=""bar-wrapper"">
                    <div class=""bar {barClass}"" style=""width: {widthPercent:F1}%""></div>
                    <span class=""bar-value"">{x.MeanSeconds:F4}s</span>
                </div>
            </div>";
                }));

            return $@"
        <div class=""product-section"">
            <h3>{SectionTitle(g.Key.Product, g.Key.Mode)}</h3>
            {bars}
        </div>";
        }));
    }

    private static string BarClass(ChartDataPoint x) => x.Product switch
    {
        "CLI" => "bar-cli",
        "PS" => "bar-ps",
        "STARTUP-CLI" => x.Mode == "cold" ? "bar-startup-cli-cold" : "bar-startup-cli",
        "STARTUP-PS" => x.Mode == "cold" ? "bar-startup-ps-cold" : "bar-startup-ps",
        _ => "bar-other"
    };

    private static (int product, int mode) SectionSort(string product, string mode)
    {
        var p = product switch
        {
            "CLI" => 0,
            "PS" => 1,
            "STARTUP-CLI" => 2,
            "STARTUP-PS" => 3,
            _ => 9
        };
        var m = mode switch { "warm" => 0, "cold" => 1, _ => 0 };
        return (p, m);
    }

    private static string SectionTitle(string product, string mode)
    {
        var label = product switch
        {
            "STARTUP-CLI" => "Startup CLI (az.exe)",
            "STARTUP-PS" => "Startup PS (pwsh.exe)",
            "CLI" => "CLI",
            "PS" => "PS",
            _ => product
        };
        return mode switch
        {
            "warm" => $"{label} — Warm (page cache hot)",
            "cold" => $"{label} — Cold (page cache evicted)",
            _ => $"{label} Benchmarks"
        };
    }

    /// <summary>
    /// Render a Warm-vs-Cold comparison table for startup benchmarks. Returns an empty string when the run
    /// produced no startup data (e.g. a CLI/PS-only run, or cold skipped because eviction was unavailable).
    /// </summary>
    private static string GenerateStartupComparison(List<ChartDataPoint> data)
    {
        var startup = data.Where(x => !string.IsNullOrEmpty(x.Mode)).ToList();
        if (startup.Count == 0) return "";

        var rows = startup
            .GroupBy(x => (Family: x.Product == "STARTUP-PS" ? "PS" : "CLI", Op: x.Extension))
            .Select(g => (
                g.Key.Family,
                g.Key.Op,
                Warm: g.FirstOrDefault(p => p.Mode == "warm")?.MeanSeconds,
                Cold: g.FirstOrDefault(p => p.Mode == "cold")?.MeanSeconds))
            .OrderBy(r => r.Family)
            .ThenBy(r => ExtOrder(r.Op))
            .ToList();

        static string Cell(double? v) => v.HasValue ? $"{v.Value:F4}s" : "—";

        static string DeltaCells(double? warm, double? cold)
        {
            if (!warm.HasValue || !cold.HasValue) return "<td>—</td><td>—</td>";
            var delta = cold.Value - warm.Value;
            var ratio = warm.Value > 0 ? cold.Value / warm.Value : 0;
            var cls = delta >= 0 ? "delta-pos" : "delta-neg";
            var sign = delta >= 0 ? "+" : "";
            return $@"<td class=""{cls}"">{sign}{delta:F4}s</td><td class=""{cls}"">{ratio:F2}×</td>";
        }

        var body = string.Join("\n", rows.Select(r => $@"
            <tr>
                <td><strong>{r.Family}</strong></td>
                <td>{r.Op}</td>
                <td>{Cell(r.Warm)}</td>
                <td>{Cell(r.Cold)}</td>
                {DeltaCells(r.Warm, r.Cold)}
            </tr>"));

        return $@"
    <div class=""chart"">
        <h2>Startup: Warm vs Cold</h2>
        <p style=""color: #666; margin-bottom: 20px;"">
            <strong>Warm</strong> = OS page cache hot (DLLs served from RAM).
            <strong>Cold</strong> = standby list purged before each launch (DLLs re-read from disk).
            A positive Δ (cold slower than warm) confirms eviction is taking effect.
        </p>
        <table class=""comparison-table"">
            <tr>
                <th>Product</th>
                <th>Operation</th>
                <th>Warm (mean)</th>
                <th>Cold (mean)</th>
                <th>Δ (Cold − Warm)</th>
                <th>Cold ÷ Warm</th>
            </tr>
            {body}
        </table>
    </div>";
    }
}

public record ChartDataPoint(string Method, string Product, double MeanSeconds, string Mode = "", string Extension = "");

internal class OverheadData
{
    public string Operation { get; set; } = "";
    public double CliTime { get; set; }
    public double PsTime { get; set; }
}
