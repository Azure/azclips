// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry;

/// <summary>
/// Replays buffered trace files to the telemetry target and deletes the ones that were
/// delivered.
/// </summary>
internal sealed class TraceUploader
{
    private const string FallbackServiceName = "AzclipsTelemetryUploader";

    private readonly TraceFileStore _store;
    private readonly ITraceExportSink _sink;

    internal TraceUploader(TraceFileStore store, ITraceExportSink sink)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    internal void Run()
    {
        var claimedFiles = _store.ClaimPending();
        if (claimedFiles.Count == 0)
            return;

        // Group by service name so each group exports under the correct cloud_RoleName.
        // Entries are flattened across a group's files so replay can link a parent and
        // child that landed in different files.
        var groups = new Dictionary<string, (List<string> Files, List<TraceEntry> Entries)>(
            StringComparer.Ordinal);

        foreach (var file in claimedFiles)
        {
            if (!_store.TryRead(file, out var content))
                continue;

            var entries = TraceFileFormat.Parse(content);
            var serviceName = (entries.Count > 0 ? entries[0].TryGetServiceName() : null)
                ?? FallbackServiceName;

            if (!groups.TryGetValue(serviceName, out var group))
            {
                group = (new List<string>(), new List<TraceEntry>());
                groups[serviceName] = group;
            }

            group.Files.Add(file);
            group.Entries.AddRange(entries);
        }

        foreach (var (serviceName, group) in groups)
        {
            // Delete only what shipped; the rest is retried next run.
            if (!_sink.TryExport(serviceName, group.Entries))
                continue;

            foreach (var file in group.Files)
                _store.Delete(file);
        }
    }
}
