// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Core.Abstractions;
using Core.Cli.Output;
using Core.Telemetry;

namespace Core.Cli;

internal sealed class CommandTiming
{
    private Action<string>? _writeDebug;
    private bool _emitted;

    public PerformanceTimeline Timeline { get; } = new("AzureCLI.Startup");

    public void Configure(OutputStreams streams, LogLevel level)
    {
        if (LoggingPolicy.IsEnabled(level, LogSeverity.Debug))
        {
            _writeDebug = message => streams.WriteDebug(message, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    public void EmitSummary(int? exitCode)
    {
        if (_emitted || _writeDebug is null) return;
        _emitted = true;
        var snapshot = Timeline.Capture();
        _writeDebug($"[CommandTiming] Managed invocation: {Milliseconds(snapshot.Elapsed)}ms; exit={exitCode?.ToString(CultureInfo.InvariantCulture) ?? "unhandled"}");
        var rows = snapshot.Measurements.Select(row => new[]
        {
            new string(' ', row.Depth * 2) + DisplayName(row.Name) + (row.IsComplete ? "" : " (incomplete)"),
            Milliseconds(row.Start),
            Milliseconds(row.End),
            Milliseconds(row.Duration),
        }).ToList();
        rows.Add(["Uninstrumented (interval union)", "-", "-", Milliseconds(snapshot.Uninstrumented)]);
        var table = TableFormatter.FormatRows(rows, ["Phase", "Start ms", "End ms", "Inclusive ms"], includeHeaderSeparator: false);
        foreach (var line in table.Split('\n'))
        {
            _writeDebug($"[CommandTiming] {line}");
        }
        _writeDebug("[CommandTiming] Durations are inclusive; nested rows and existing routing/DLL timings overlap. Do not add them.");
        _writeDebug("[CommandTiming] Excludes native/.NET startup before managed entry, this summary, later runtime teardown, and detached uploader completion.");
    }

    private static string Milliseconds(TimeSpan elapsed) =>
        elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);

    private static string DisplayName(string name) => name switch
    {
        "AzureCLI.Startup.Container" => "Container setup",
        "AzureCLI.Startup.Services" => "Service resolution",
        "AzureCLI.ParseGlobalArgs" => "Global argument parsing",
        "AzureCLI.Startup.Telemetry" => "Session/telemetry setup",
        "AzureCLI.HandleGlobalArgs" => "Global argument handling",
        "AzureCLI.ParseCommandInvocation" => "Command-tree construction",
        "AzureCLI.ProcessCommand" => "Execution session",
        "AzureCLI.DispatchHelp" => "Help",
        "AzureCLI.DispatchVersion" => "Version",
        _ => name,
    };
}
