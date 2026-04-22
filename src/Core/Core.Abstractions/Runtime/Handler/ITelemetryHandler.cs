namespace Core.Abstractions.Runtime.Handler
{
    using System.Diagnostics;

    /// <summary>
    /// Marker interface for telemetry handlers.
    /// </summary>
    public interface ITelemetryHandler
    {
        StartActivityDelegate StartActivity { get; set; }
    }
}