namespace Core.Abstractions.Runtime.Handler
{
    using System.Diagnostics;

    /// <summary>
    /// Marker interface for command telemetry handlers.
    /// </summary>
    public interface ICommandTelemetryHandler
    {
        StartActivityDelegate StartActivity { get; set; }
    }
}