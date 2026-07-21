// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;

namespace Core.Runtime;

/// <summary>
/// Simple console-based telemetry logger.
/// </summary>
public class ConsoleLoggingService : ILoggingService
{
    // Alias for `Info`; will be removed in a future release.
    public void Log(string message) => Info(message);

    public void Info(string message)
    {
        Console.Error.WriteLine($"[INFO] {message}");
        RecordTelemetry("Info", message);
    }

    public void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
        RecordTelemetry("Error", message);
    }

    public void Warn(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Error.WriteLine($"[WARN] {message}");
        Console.ResetColor();
        RecordTelemetry("Warning", message);
    }

    public void Verbose(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Error.WriteLine($"[VERBOSE] {message}");
        Console.ResetColor();
        RecordTelemetry("Verbose", message);
    }

    public void Debug(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Error.WriteLine($"[DEBUG] {message}");
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
