// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Benchmark;

/// <summary>
/// Exports benchmark results to Application Insights as custom metrics.
/// Enables Grafana dashboards using Azure Monitor data source.
/// </summary>
public class AppInsightsExporter : IDisposable
{
    private readonly TelemetryClient _telemetryClient;
    private readonly TelemetryConfiguration _config;

    // Default connection string from the project
    private const string DefaultConnectionString = 
        "InstrumentationKey=75095e49-0ed7-43bf-9a9b-145ce55963c5;" +
        "IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;" +
        "LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;" +
        "ApplicationId=c4b7bab3-d4b6-4f80-8440-0f529a622b2f";

    public AppInsightsExporter(string? connectionString = null)
    {
        _config = TelemetryConfiguration.CreateDefault();
        _config.ConnectionString = connectionString ?? 
            Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING") ?? 
            DefaultConnectionString;
        _telemetryClient = new TelemetryClient(_config);
    }

    /// <summary>
    /// Export benchmark results to App Insights.
    /// </summary>
    public void Export(IEnumerable<ChartDataPoint> results, string timestamp)
    {
        Console.WriteLine("\n[AppInsights] Exporting benchmark results...");

        foreach (var result in results)
        {
            var (operation, resource) = ParseMethod(result.Method);

            // Send as custom metric
            var metric = new MetricTelemetry
            {
                Name = "BenchmarkDuration",
                Sum = result.MeanSeconds,
                Count = 1
            };

            // Custom dimensions for Grafana filtering
            metric.Properties["Product"] = result.Product;           // CLI, PS
            metric.Properties["Operation"] = operation;              // List, Create, Get
            metric.Properties["Resource"] = resource;                // ResourceGroup
            metric.Properties["Method"] = result.Method;             // Full method name
            metric.Properties["Timestamp"] = timestamp;
            metric.Properties["Machine"] = Environment.MachineName;

            _telemetryClient.TrackMetric(metric);

            // Also send as custom event for easier querying
            var eventTelemetry = new EventTelemetry("BenchmarkResult");
            eventTelemetry.Properties["Product"] = result.Product;
            eventTelemetry.Properties["Operation"] = operation;
            eventTelemetry.Properties["Resource"] = resource;
            eventTelemetry.Properties["Method"] = result.Method;
            eventTelemetry.Properties["Timestamp"] = timestamp;
            eventTelemetry.Metrics["DurationSeconds"] = result.MeanSeconds;
            eventTelemetry.Metrics["DurationMs"] = result.MeanSeconds * 1000;

            _telemetryClient.TrackEvent(eventTelemetry);

            Console.WriteLine($"[AppInsights] {result.Product}/{operation}: {result.MeanSeconds:F4}s");
        }

        // Flush to ensure data is sent
        Console.WriteLine("[AppInsights] Flushing telemetry...");
        _telemetryClient.Flush();
        
        // Wait longer for flush to complete (App Insights can be slow)
        Task.Delay(5000).Wait();
        
        Console.WriteLine($"[AppInsights] Export complete. Data sent to: {_config.ConnectionString?[..50]}...");
        Console.WriteLine("[AppInsights] Check in Azure Portal: Application Insights > Logs > customMetrics or customEvents");
    }

    private static (string operation, string resource) ParseMethod(string method)
    {
        var operation = "Unknown";
        var resource = "ResourceGroup";

        var lowerMethod = method.ToLowerInvariant();
        
        if (lowerMethod.Contains("list"))
            operation = "List";
        else if (lowerMethod.Contains("create") || lowerMethod.Contains("new"))
            operation = "Create";
        else if (lowerMethod.Contains("get") || lowerMethod.Contains("show"))
            operation = "Get";
        else if (lowerMethod.Contains("delete") || lowerMethod.Contains("remove"))
            operation = "Delete";

        return (operation, resource);
    }

    public void Dispose()
    {
        _telemetryClient.Flush();
        _config.Dispose();
    }
}
