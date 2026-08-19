// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Telemetry sender for CLI: buffers traces to local files via FileExporter,
/// then replays them to the resolved telemetry target (local OTLP or Azure Monitor)
/// in a background process.
/// Also used by CLI's --upload-telemetry spawned process.
/// </summary>
public class FileTelemetrySender : ITelemetrySender
{
    private const string FallbackSourceName = "AzclipsTelemetryUploader";

    /// <summary>
    /// Gets the shared trace directory for the background uploader picks up all files from one location.
    /// Uses the same <c>{LocalApplicationData}/.azclips/</c> root as auth profiles for consistency.
    /// </summary>
    public static string GetTraceDirectory() => TraceFileStore.DefaultDirectory;

    public void ConfigureExporter(TracerProviderBuilder builder, string traceDirectory)
    {
        builder.AddProcessor(new SimpleActivityExportProcessor(new FileExporter(traceDirectory)));
    }

    Task ITelemetrySender.SendPendingAsync() => SendPendingStatic();

    /// <summary>
    /// Sends all pending trace files to the resolved telemetry target
    /// (local OTLP or Azure Monitor), then deletes them.
    /// Static entry point for CLI's --upload-telemetry spawned process.
    /// </summary>
    public static Task SendPendingStatic()
    {
        var store = new TraceFileStore(TraceFileStore.DefaultDirectory);

        var claimedFiles = store.ClaimPending();

        if (claimedFiles.Count == 0)
            return Task.CompletedTask;

        // Group traces by original service name (CLI/PowerShell) so each group
        // is uploaded with the correct cloud_RoleName in App Insights.
        var tracesByService = new Dictionary<string, List<(string file, List<TraceEntry> traces)>>();

        foreach (var file in claimedFiles)
        {
            if (!store.TryRead(file, out var content))
                continue;

            var traces = TraceFileFormat.Parse(content);
            var serviceName = (traces.Count > 0 ? traces[0].TryGetServiceName() : null)
                ?? FallbackSourceName;

            if (!tracesByService.ContainsKey(serviceName))
                tracesByService[serviceName] = new();
            tracesByService[serviceName].Add((file, traces));
        }

        var replayedFiles = new List<string>();

        // Resolve once so every service group in this replay uses the same destination.
        var target = TelemetryTargetResolver.Resolve();

        foreach (var (serviceName, fileGroup) in tracesByService)
        {
            using var source = new ActivitySource(serviceName);
            var builder = Sdk.CreateTracerProviderBuilder()
                .AddSource(serviceName)
                .ConfigureResource(r => r.AddService(serviceName: serviceName));
            TelemetryExporters.AddTraceExporter(builder, target);
            using var provider = builder.Build()!;

            // Flatten across all files in the group so the topological replay can
            // resolve parent/child links even when they live in different files.
            var allEntries = fileGroup.SelectMany(f => f.traces);
            TraceReplayer.ReplayEntries(source, allEntries);

            // Only mark files for deletion if flush succeeds within the timeout.
            // If flush fails/times out, files remain on disk for the next upload attempt.
            if (provider.ForceFlush(timeoutMilliseconds: 10_000))
            {
                foreach (var (file, _) in fileGroup)
                    replayedFiles.Add(file);
            }
        }

        foreach (var file in replayedFiles)
        {
            store.Delete(file);
        }

        return Task.CompletedTask;
    }

}
