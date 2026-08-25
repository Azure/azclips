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
    // Alias for `Info`; kept as a permanent alias on the interface for backward compatibility
    // (no deprecation planned). See PR #575 / issue Azure/CLIPS#493, decision D3.
    public void Log(string message) => Info(message);

    public void Info(string message)
    {
        ConsoleSink.Info(message);
        RecordTelemetry("Info", message);
    }

    public void Error(string message)
    {
        ConsoleSink.Error(message);
        RecordTelemetry("Error", message);
    }

    public void Warn(string message)
    {
        ConsoleSink.Warn(message);
        RecordTelemetry("Warning", message);
    }

    public void Verbose(string message)
    {
        ConsoleSink.Verbose(message);
        RecordTelemetry("Verbose", message);
    }

    public void Debug(string message)
    {
        ConsoleSink.Debug(message);
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
