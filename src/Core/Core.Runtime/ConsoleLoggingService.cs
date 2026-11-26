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
        RecordTelemetry("Info");
    }

    public void Error(string message)
    {
        ConsoleSink.Error(message);
        RecordTelemetry("Error");
    }

    public void Warn(string message)
    {
        ConsoleSink.Warn(message);
        RecordTelemetry("Warning");
    }

    public void Verbose(string message)
    {
        ConsoleSink.Verbose(message);
        RecordTelemetry("Verbose");
    }

    public void Debug(string message)
    {
        ConsoleSink.Debug(message);
        RecordTelemetry("Debug");
    }

    private void RecordTelemetry(string level)
    {
        var tags = new ActivityTagsCollection
        {
            { "level", level },
            { "logger", nameof(ConsoleLoggingService) }
        };

        // Attach log to the current activity (trace) if available
        Activity.Current?.AddEvent(new ActivityEvent(level, tags: tags));
    }
}
