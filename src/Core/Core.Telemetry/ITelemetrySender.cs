using System.Threading.Tasks;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Abstracts how telemetry is exported to Azure Monitor.
/// CLI uses file-based buffering + background replay; PS exports inline.
/// </summary>
public interface ITelemetrySender
{
    /// <summary>
    /// Adds the appropriate exporter(s) to the TracerProvider being built.
    /// </summary>
    void ConfigureExporter(TracerProviderBuilder builder, string traceDirectory);

    /// <summary>
    /// Performs any post-command telemetry work (e.g. replaying buffered files).
    /// Called after the activity is disposed.
    /// </summary>
    Task SendPendingAsync();
}
