using System;
using System.Diagnostics;
using Core.Abstractions;

namespace Core.Implementation;

/// <summary>
/// Simple console-based telemetry service for logging.
/// </summary>
public class ConsoleLoggerService : ILoggerService
{
    private readonly ITelemetryService _telemetryService;

    public ConsoleLoggerService(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
    }

    public void Log(string message)
    {
        // Silent for clean output - can be enabled for debugging
        Console.WriteLine($"[LOG] {message}");
        RecordTelemetry("Log", message);
    }

    public void LogInfo(string message)
    {
        // Silent for clean output - can be enabled for debugging
        Console.WriteLine($"[INFO] {message}");
        RecordTelemetry("Info", message);
    }

    public void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {message}");
        Console.ResetColor();
        RecordTelemetry("Error", message);
    }

    public void LogWarning(string message)
    {
        // Silent for clean output
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARN] {message}");
        Console.ResetColor();
        RecordTelemetry("Warning", message);
    }

    private void RecordTelemetry(string level, string message)
    {
        var tags = new ActivityTagsCollection
        {
            { "message", message },
            { "level", level },
            { "logger", nameof(ConsoleLoggerService) }
        };

        // Attach log to the current activity (trace)
        Activity.Current?.AddEvent(new ActivityEvent(level, tags: tags));
    }
}
