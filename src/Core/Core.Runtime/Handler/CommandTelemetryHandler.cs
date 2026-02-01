namespace Core.Abstractions.Runtime.Handler
{
    using System.Diagnostics;

    /// <summary>
    /// Marker interface for command telemetry handlers.
    /// </summary>
    public class CommandTelemetryHandler: ICommandTelemetryHandler
    {
        public StartActivityDelegate StartActivity { get; set; } = null!;
    }
}