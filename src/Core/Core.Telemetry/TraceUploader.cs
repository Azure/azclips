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

        // Group by service and persisted data boundary so each replay batch exports
        // under the right cloud_RoleName and Aria ingestion endpoint.
        var groups = new Dictionary<UploadGroupKey, (HashSet<string> Files, List<TraceEntry> Entries)>();
        var fileGroups = new Dictionary<string, HashSet<UploadGroupKey>>(StringComparer.Ordinal);

        foreach (var file in claimedFiles)
        {
            if (!_store.TryRead(file, out var content))
                continue;

            var entries = TraceFileFormat.Parse(content);
            AddFileEntries(file, entries, groups, fileGroups);
        }

        var deliveredGroups = new HashSet<UploadGroupKey>();
        foreach (var (key, group) in groups)
        {
            if (!_sink.TryExport(key.ServiceName, group.Entries, key.DataBoundary))
                continue;

            deliveredGroups.Add(key);
        }

        foreach (var (file, keys) in fileGroups)
        {
            if (keys.All(deliveredGroups.Contains))
                _store.Delete(file);
        }
    }

    private static void AddFileEntries(
        string file,
        IReadOnlyList<TraceEntry> entries,
        Dictionary<UploadGroupKey, (HashSet<string> Files, List<TraceEntry> Entries)> groups,
        Dictionary<string, HashSet<UploadGroupKey>> fileGroups)
    {
        var serviceName = (entries.Count > 0 ? entries[0].TryGetServiceName() : null)
            ?? FallbackServiceName;

        if (entries.Count == 0)
        {
            AddGroup(file, new UploadGroupKey(serviceName, DataBoundaryRouting.Unknown), [], groups, fileGroups);
            return;
        }

        foreach (var boundaryGroup in entries.GroupBy(e => DataBoundaryRouting.Normalize(e.DataBoundary)))
        {
            AddGroup(
                file,
                new UploadGroupKey(serviceName, boundaryGroup.Key),
                boundaryGroup.ToList(),
                groups,
                fileGroups);
        }
    }

    private static void AddGroup(
        string file,
        UploadGroupKey key,
        IReadOnlyList<TraceEntry> entries,
        Dictionary<UploadGroupKey, (HashSet<string> Files, List<TraceEntry> Entries)> groups,
        Dictionary<string, HashSet<UploadGroupKey>> fileGroups)
    {
        if (!groups.TryGetValue(key, out var group))
        {
            group = (new HashSet<string>(StringComparer.Ordinal), new List<TraceEntry>());
            groups[key] = group;
        }

        group.Files.Add(file);
        group.Entries.AddRange(entries);

        if (!fileGroups.TryGetValue(file, out var keys))
        {
            keys = new HashSet<UploadGroupKey>();
            fileGroups[file] = keys;
        }
        keys.Add(key);
    }

    private readonly record struct UploadGroupKey(string ServiceName, string DataBoundary);
}
