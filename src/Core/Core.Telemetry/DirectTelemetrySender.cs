using System.Threading.Tasks;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Telemetry sender for PowerShell: exports traces directly to Azure Monitor
/// via the TracerProvider pipeline. No file buffering or replay needed.
/// </summary>
public class DirectTelemetrySender : ITelemetrySender
{
    public void ConfigureExporter(TracerProviderBuilder builder, string traceDirectory)
    {
        builder.AddAzureMonitorTraceExporter(options =>
        {
            options.ConnectionString = WellKnownEndpoints.AzureMonitorConnectionString;
        });
    }

    /// <summary>
    /// No-op: telemetry is already exported inline by the TracerProvider.
    /// </summary>
    public Task SendPendingAsync() => Task.CompletedTask;
}
