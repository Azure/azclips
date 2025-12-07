namespace Core.Abstractions;

/// <summary>
/// Represents a telemetry service for logging and tracking.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Logs an informational message.
    /// </summary>
    void LogInfo(string message);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    void LogError(string message, Exception? exception = null);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    void LogWarning(string message);
}
