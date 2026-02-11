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
    /// <param name="exportToCloud">If true, exports to App Insights. Set false for local-only runs.</param>
    public static void Generate(string artifactsPath, string timestamp, IEnumerable<Summary> summaries, bool exportToCloud = true)
    {
        var chartData = ExtractChartData(summaries);
        if (chartData.Count == 0) return;

        var overheadData = CalculateOverhead(chartData);

        // Generate professional grouped bar chart (Chart.js)
        var chartJsPath = Path.Combine(artifactsPath, $"benchmark-comparison-{timestamp}.html");
        File.WriteAllText(chartJsPath, GenerateGroupedBarChart(overheadData, timestamp));
        Console.WriteLine($"\n[Chart] Generated: {chartJsPath}");

        // Generate detailed HTML chart
        var htmlPath = Path.Combine(artifactsPath, $"benchmark-details-{timestamp}.html");
        File.WriteAllText(htmlPath, GenerateDetailsChart(chartData, overheadData, timestamp));
        Console.WriteLine($"[Chart] Details: {htmlPath}");

        // Generate JSON summary
        var jsonPath = Path.Combine(artifactsPath, $"benchmark-summary-{timestamp}.json");
        var jsonData = new
        {
            Timestamp = timestamp,
            Results = chartData.Select(x => new { x.Method, x.Product, MeanSeconds = Math.Round(x.MeanSeconds, 4) }),
            Overhead = overheadData.Select(x => new { x.Operation, x.ApiTime, x.CliTime, x.PsTime })
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"[Chart] Summary JSON: {jsonPath}");

        // Export to Application Insights for Grafana (skip in local mode)
        if (exportToCloud)
        {
            ExportToAppInsights(chartData, timestamp);
        }
        else
        {
            Console.WriteLine("[AppInsights] Export skipped (local mode)");
        }
    }

    /// <summary>
    /// Export benchmark results to Application Insights.
    /// </summary>
    private static void ExportToAppInsights(List<ChartDataPoint> chartData, string timestamp)
    {
        try
        {
            using var exporter = new AppInsightsExporter();
            exporter.Export(chartData, timestamp);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppInsights] Export failed: {ex.Message}");
        }
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
                var product = fullName.Contains(".API.") ? "API" :
                              fullName.Contains(".CLI.") ? "CLI" :
                              fullName.Contains(".PS.") ? "PS" : "Other";
                var meanNs = report.ResultStatistics.Mean;
                var meanSeconds = meanNs / 1_000_000_000.0;

                chartData.Add(new ChartDataPoint(methodName, product, meanSeconds));
            }
        }

        return chartData;
    }

    private static List<OverheadData> CalculateOverhead(List<ChartDataPoint> data)
    {
        var operations = new[] { "List RG", "Create RG", "Get RG" };

        return operations.Select(op =>
        {
            var apiResult = data.FirstOrDefault(x => x.Method.Contains(op) && x.Product == "API");
            var cliResult = data.FirstOrDefault(x => x.Method.Contains(op) && x.Product == "CLI");
            var psResult = data.FirstOrDefault(x => x.Method.Contains(op) && x.Product == "PS");

            return new OverheadData
            {
                Operation = op,
                ApiTime = Math.Round(apiResult?.MeanSeconds ?? 0, 4),
                CliTime = Math.Round(cliResult?.MeanSeconds ?? 0, 4),
                PsTime = Math.Round(psResult?.MeanSeconds ?? 0, 4)
            };
        }).ToList();
    }

    private static string GenerateGroupedBarChart(List<OverheadData> overheadData, string timestamp)
    {
        var operations = overheadData.Select(x => $"'{x.Operation}'").ToArray();
        var apiData = overheadData.Select(x => x.ApiTime.ToString("F4")).ToArray();
        var cliData = overheadData.Select(x => x.CliTime.ToString("F4")).ToArray();
        var psData = overheadData.Select(x => x.PsTime.ToString("F4")).ToArray();

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
        .stat-value.api {{ color: #10b981; }}
        .stat-value.cli {{ color: #3b82f6; }}
        .stat-value.ps {{ color: #8b5cf6; }}
        .overhead {{
            color: #ef4444;
            font-size: 11px;
            margin-left: 5px;
        }}
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
        <p class=""subtitle"">Execution time comparison across API, CLI, and PowerShell for Resource Group operations</p>

        <div class=""chart-container"">
            <div class=""chart-wrapper"">
                <canvas id=""comparisonChart""></canvas>
            </div>
        </div>

        <div class=""stats-grid"">
            {string.Join("\n", overheadData.Select(x => $@"
            <div class=""stat-card"">
                <h3>{x.Operation}</h3>
                <div class=""stat-row"">
                    <span class=""stat-label"">API (HTTP Pipeline)</span>
                    <span class=""stat-value api"">{x.ApiTime:F4}s</span>
                </div>
                <div class=""stat-row"">
                    <span class=""stat-label"">CLI (azv2.exe)</span>
                    <span class=""stat-value cli"">{x.CliTime:F4}s
                        <span class=""overhead"">{(x.CliTime > 0 && x.ApiTime > 0 ? $"+{(x.CliTime - x.ApiTime):F2}s" : "")}</span>
                    </span>
                </div>
                <div class=""stat-row"">
                    <span class=""stat-label"">PS (pwsh.exe)</span>
                    <span class=""stat-value ps"">{x.PsTime:F4}s
                        <span class=""overhead"">{(x.PsTime > 0 && x.ApiTime > 0 ? $"+{(x.PsTime - x.ApiTime):F2}s" : "")}</span>
                    </span>
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
                        label: 'API (HTTP Pipeline)',
                        data: [{string.Join(", ", apiData)}],
                        backgroundColor: 'rgba(16, 185, 129, 0.8)',
                        borderColor: 'rgba(16, 185, 129, 1)',
                        borderWidth: 1,
                        borderRadius: 4
                    }},
                    {{
                        label: 'CLI (azv2.exe)',
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
        List<OverheadData> overheadData,
        string timestamp)
    {
        var maxValue = data.Max(x => x.MeanSeconds);

        // Group by product
        var byProduct = data.GroupBy(x => x.Product).OrderBy(g => g.Key);

        var productSections = string.Join("\n", byProduct.Select(g =>
        {
            var bars = string.Join("\n", g.Select(x =>
            {
                var widthPercent = (x.MeanSeconds / maxValue) * 100;
                var barClass = x.Product switch
                {
                    "API" => "bar-api",
                    "CLI" => "bar-cli",
                    "PS" => "bar-ps",
                    _ => "bar-other"
                };
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
            <h3>{g.Key} Benchmarks</h3>
            {bars}
        </div>";
        }));

        // Comparison table
        var comparisonRows = string.Join("\n", overheadData.Where(x => x.ApiTime > 0).Select(x => $@"
            <tr>
                <td><strong>{x.Operation}</strong></td>
                <td>{x.ApiTime:F4}s</td>
                <td>{x.CliTime:F4}s <span class=""overhead"">({(x.CliTime > 0 && x.ApiTime > 0 ? $"+{(x.CliTime - x.ApiTime):F4}s" : "N/A")})</span></td>
                <td>{x.PsTime:F4}s <span class=""overhead"">({(x.PsTime > 0 && x.ApiTime > 0 ? $"+{(x.PsTime - x.ApiTime):F4}s" : "N/A")})</span></td>
            </tr>"));

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
        .bar-api {{ background: linear-gradient(90deg, #4CAF50, #8BC34A); }}
        .bar-cli {{ background: linear-gradient(90deg, #2196F3, #64B5F6); }}
        .bar-ps {{ background: linear-gradient(90deg, #9C27B0, #BA68C8); }}
        .bar-value {{ margin-left: 10px; font-weight: bold; color: #333; white-space: nowrap; }}
        .legend {{ display: flex; gap: 20px; margin-top: 20px; flex-wrap: wrap; }}
        .legend-item {{ display: flex; align-items: center; gap: 5px; }}
        .legend-color {{ width: 20px; height: 20px; border-radius: 3px; }}
        .comparison-table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
        .comparison-table th, .comparison-table td {{ padding: 12px; text-align: left; border-bottom: 1px solid #eee; }}
        .comparison-table th {{ background: #f9f9f9; font-weight: 600; }}
        .overhead {{ color: #999; font-size: 12px; }}
        .timestamp {{ color: #999; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <h1>📊 Benchmark Details</h1>

    <div class=""chart"">
        <h2>Execution Time by Product</h2>
        {productSections}
        <div class=""legend"">
            <div class=""legend-item""><div class=""legend-color"" style=""background: #4CAF50;""></div> API (HTTP Pipeline)</div>
            <div class=""legend-item""><div class=""legend-color"" style=""background: #2196F3;""></div> CLI (azv2.exe)</div>
            <div class=""legend-item""><div class=""legend-color"" style=""background: #9C27B0;""></div> PS (pwsh.exe)</div>
        </div>
    </div>

    <div class=""chart"">
        <h2>Cross-Product Comparison</h2>
        <p style=""color: #666; margin-bottom: 20px;"">Shows overhead compared to pure API time for each operation.</p>
        <table class=""comparison-table"">
            <tr>
                <th>Operation</th>
                <th>API</th>
                <th>CLI</th>
                <th>PS</th>
            </tr>
            {comparisonRows}
        </table>
    </div>

    <p class=""timestamp"">Generated: {timestamp}</p>
</body>
</html>";
    }
}

public record ChartDataPoint(string Method, string Product, double MeanSeconds);

internal class OverheadData
{
    public string Operation { get; set; } = "";
    public double ApiTime { get; set; }
    public double CliTime { get; set; }
    public double PsTime { get; set; }
}
