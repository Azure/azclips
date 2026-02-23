using System;
using System.Diagnostics;
using Core.Abstractions;

namespace Core.Implementation;

/// <summary>
/// Simple console-based telemetry logger.
/// </summary>
public class ConsoleLoggingService(IConfigurationProvider Config) : ILoggingService
{
    public void Log(string message)
    {
        Console.WriteLine($"{message}");
        RecordTelemetry("Log", message);
    }

    public void Info(string message)
    {
        if (Config.Get("CIRRUS_INFO")?.Equals("true", StringComparison.InvariantCultureIgnoreCase) != true)
        {
            return; // Skip info logs unless enabled via configuration
        }
        Console.WriteLine($"[INFO] {message}");
        RecordTelemetry("Info", message);
    }

    public void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
        RecordTelemetry("Error", message);
    }

    public void Warn(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARN] {message}");
        Console.ResetColor();
        RecordTelemetry("Warning", message);
    }

    public void Verbose(string message)
    {
        if (Config.Get("CIRRUS_VERBOSE")?.Equals("true", StringComparison.InvariantCultureIgnoreCase) != true)
        {
            return; // Skip verbose logs unless enabled via configuration
        }
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[VERBOSE] {message}");
        Console.ResetColor();
        RecordTelemetry("Verbose", message);
    }

    public void Debug(string message)
    {
        if (Config.Get("CIRRUS_DEBUG")?.Equals("true", StringComparison.InvariantCultureIgnoreCase) != true)
        {
            return; // Skip debug logs unless enabled via configuration
        }
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[DEBUG] {message}");
        Console.ResetColor();
        RecordTelemetry("Debug", message);
    }

    private void RecordTelemetry(string level, string message)
    {
        var tags = new ActivityTagsCollection
        {
            { "message", message },
            { "level", level },
            { "logger", nameof(ConsoleLoggingService) }
        };

        // Attach log to the current activity (trace) if available
        Activity.Current?.AddEvent(new ActivityEvent(level, tags: tags));
    }
}
