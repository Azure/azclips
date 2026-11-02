// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Core.Abstractions;

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
    private readonly IReadOnlyDictionary<string, TelemetryLayer> _layers;

    internal TraceUploader(
        TraceFileStore store,
        ITraceExportSink sink,
        IEnumerable<TelemetryLayer>? layers = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _layers = (layers ?? TelemetryLayers.All).ToDictionary(layer => layer.FileToken, StringComparer.Ordinal);
    }

    internal void Run()
    {
        // Keep stale-claim recovery from stealing a live uploader's files during a slow export.
        using var upload = _store.TryStartUpload();
        if (upload is null)
            return;
        var claimedFiles = _store.ClaimPending(out var reservationByClaimedFile);
        if (claimedFiles.Count == 0)
            return;

        // Group by service and recorded routing metadata, never the current active profile.
        var groups = new Dictionary<UploadGroupKey, (HashSet<string> Files, List<TraceEntry> Entries)>();
        var fileGroups = new Dictionary<string, HashSet<UploadGroupKey>>(StringComparer.Ordinal);
        var failedReservations = new HashSet<string>(StringComparer.Ordinal);
        // A rejected layer file leaves its reservation; the CLI files still upload.
        var rejectedFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in claimedFiles)
        {
            void FailReservation()
            {
                if (reservationByClaimedFile.TryGetValue(file, out var reservation))
                    failedReservations.Add(reservation);
            }

            if (!_store.TryRead(file, out var content))
            {
                FailReservation();
                continue;
            }

            var profile = TraceReplayProfile.Legacy;
            List<TraceEntry> entries;
            if (TraceFileStore.IsLayerFile(file))
            {
                if (!TraceFileStore.TryGetLayerToken(file, out var token) ||
                    !_layers.TryGetValue(token, out var layer))
                {
                    _store.Reject(file, "unknown_layer_file");
                    rejectedFiles.Add(file);
                    continue;
                }
                if (!TraceFileFormat.TryParseComplete(content, out entries) ||
                    !ValidateLayerEntries(entries, layer))
                {
                    _store.Reject(file, "invalid_layer_records");
                    rejectedFiles.Add(file);
                    continue;
                }
                profile = TraceReplayProfile.ManagedLayer;
            }
            else
            {
                entries = TraceFileFormat.Parse(content);
            }

            AddFileEntries(file, entries, profile, groups, fileGroups);
        }

        var replayGroups = groups;
        var replayBatches = BuildReplayBatches(replayGroups);
        var deliveredBatches = new HashSet<ReplayBatchKey>();
        var replayContext = new TraceReplayContext();
        foreach (var batch in replayBatches)
        {
            if (batch.Dependencies.Any(parent => !deliveredBatches.Contains(parent)) ||
                !_sink.TryExport(batch.Key.Group.ServiceName, batch.Entries,
                    batch.Key.Group.DataBoundary, batch.Key.Group.Profile, replayContext))
                continue;

            deliveredBatches.Add(batch.Key);
        }

        var deliveredGroups = replayGroups.Keys.Where(group =>
            replayBatches.Where(batch => batch.Key.Group == group)
                .All(batch => deliveredBatches.Contains(batch.Key))
        ).ToHashSet();

        foreach (var (file, keys) in fileGroups)
        {
            if (!reservationByClaimedFile.ContainsKey(file) && keys.All(deliveredGroups.Contains))
            {
                _store.Delete(file);
            }
        }

        foreach (var reservationGroup in reservationByClaimedFile
                     .GroupBy(pair => pair.Value, StringComparer.Ordinal))
        {
            if (failedReservations.Contains(reservationGroup.Key))
                continue;

            var files = reservationGroup.Select(pair => pair.Key)
                .Where(file => !rejectedFiles.Contains(file)).ToArray();
            if (files.Any(file => !fileGroups.TryGetValue(file, out var keys) ||
                                  !keys.All(deliveredGroups.Contains)))
                continue;

            if (!_store.DeleteReservation(reservationGroup.Key))
                continue;
            foreach (var file in files)
            {
                _store.Delete(file);
            }
        }
    }

    private static IReadOnlyList<ReplayBatch> BuildReplayBatches(
        Dictionary<UploadGroupKey, (HashSet<string> Files, List<TraceEntry> Entries)> groups)
    {
        if (TryBuildGroupBatches(groups, out var groupBatches))
            return groupBatches;

        var nodes = groups.SelectMany(pair => pair.Value.Entries.Select(entry =>
                new ReplayNode(pair.Key, entry)))
            .ToList();
        var owners = new Dictionary<ReplaySpanKey, int>();
        for (var index = 0; index < nodes.Count; index++)
        {
            if (nodes[index].Identity is { } identity)
                owners.TryAdd(identity, index);
        }

        var children = Enumerable.Range(0, nodes.Count).Select(_ => new List<int>()).ToArray();
        var remainingParents = new int[nodes.Count];
        for (var index = 0; index < nodes.Count; index++)
        {
            if (nodes[index].ParentIdentity is not { } parentIdentity ||
                !owners.TryGetValue(parentIdentity, out var parent) || parent == index)
                continue;
            nodes[index].ParentIndex = parent;
            children[parent].Add(index);
            remainingParents[index]++;
        }

        var ready = new Queue<int>(Enumerable.Range(0, nodes.Count)
            .Where(index => remainingParents[index] == 0));
        var ordered = new List<int>(nodes.Count);
        while (ready.Count > 0)
        {
            var parent = ready.Dequeue();
            ordered.Add(parent);
            foreach (var child in children[parent])
            {
                nodes[child].Rank = Math.Max(nodes[child].Rank, nodes[parent].Rank + 1);
                if (--remainingParents[child] == 0)
                    ready.Enqueue(child);
            }
        }

        // Cyclic or invalid parent chains are replayed after valid roots. Their unresolved
        // edges retain the profile's existing best-effort fallback behavior.
        var unresolvedRank = nodes.Count == ordered.Count
            ? 0
            : nodes.Where((_, index) => remainingParents[index] > 0)
                .Select(node => node.Rank).DefaultIfEmpty(0).Max() + 1;
        for (var index = 0; index < nodes.Count; index++)
        {
            if (remainingParents[index] > 0)
            {
                nodes[index].Rank = unresolvedRank;
                ordered.Add(index);
            }
        }

        var batchMap = new Dictionary<ReplayBatchKey, ReplayBatch>();
        foreach (var index in ordered)
        {
            var node = nodes[index];
            var key = new ReplayBatchKey(node.Rank, node.Group);
            if (!batchMap.TryGetValue(key, out var batch))
                batchMap[key] = batch = new ReplayBatch(key);
            batch.Entries.Add(node.Entry);
        }

        foreach (var (group, data) in groups)
        {
            if (data.Entries.Count == 0)
            {
                var key = new ReplayBatchKey(0, group);
                batchMap.TryAdd(key, new ReplayBatch(key));
            }
        }

        foreach (var (childIndex, node) in nodes.Select((node, index) => (index, node)))
        {
            if (node.ParentIndex is not { } parentIndex ||
                nodes[parentIndex].Rank >= node.Rank)
                continue;
            var childKey = new ReplayBatchKey(node.Rank, node.Group);
            var parent = nodes[parentIndex];
            batchMap[childKey].Dependencies.Add(new ReplayBatchKey(parent.Rank, parent.Group));
        }

        return batchMap.Values
            .OrderBy(batch => batch.Key.Rank)
            .ThenBy(batch => batch.Key.Group.ServiceName, StringComparer.Ordinal)
            .ThenBy(batch => batch.Key.Group.DataBoundary, StringComparer.Ordinal)
            .ThenBy(batch => batch.Key.Group.DataBoundaryOutcome, StringComparer.Ordinal)
            .ThenBy(batch => batch.Key.Group.Profile)
            .ToArray();
    }

    private static bool TryBuildGroupBatches(
        Dictionary<UploadGroupKey, (HashSet<string> Files, List<TraceEntry> Entries)> groups,
        out IReadOnlyList<ReplayBatch> batches)
    {
        var owners = new Dictionary<ReplaySpanKey, UploadGroupKey>();
        foreach (var (group, data) in groups)
        {
            foreach (var entry in data.Entries)
            {
                if (!string.IsNullOrEmpty(entry.TraceId) && !string.IsNullOrEmpty(entry.SpanId))
                    owners.TryAdd(new ReplaySpanKey(entry.TraceId, entry.SpanId, group.DataBoundary, group.DataBoundaryOutcome), group);
            }
        }

        var dependencies = groups.Keys.ToDictionary(group => group, _ => new HashSet<UploadGroupKey>());
        foreach (var (group, data) in groups)
        {
            foreach (var entry in data.Entries)
            {
                if (string.IsNullOrEmpty(entry.TraceId) ||
                    string.IsNullOrEmpty(entry.ParentSpanId) ||
                    entry.ParentSpanId == "0000000000000000" ||
                    !owners.TryGetValue(new ReplaySpanKey(entry.TraceId, entry.ParentSpanId, group.DataBoundary, group.DataBoundaryOutcome),
                        out var parent) ||
                    parent == group)
                    continue;
                dependencies[group].Add(parent);
            }
        }

        var remaining = groups.Keys.ToDictionary(group => group, group => dependencies[group].Count);
        var ready = new Queue<UploadGroupKey>(groups.Keys.Where(group => remaining[group] == 0));
        var ordered = new List<UploadGroupKey>(groups.Count);
        while (ready.Count > 0)
        {
            var parent = ready.Dequeue();
            ordered.Add(parent);
            foreach (var child in groups.Keys.Where(group => dependencies[group].Contains(parent)))
            {
                if (--remaining[child] == 0)
                    ready.Enqueue(child);
            }
        }

        if (ordered.Count != groups.Count)
        {
            batches = [];
            return false;
        }

        var rankByGroup = ordered.Select((group, rank) => (group, rank))
            .ToDictionary(item => item.group, item => item.rank);
        var groupBatches = new Dictionary<UploadGroupKey, ReplayBatch>();
        foreach (var group in ordered)
        {
            var key = new ReplayBatchKey(rankByGroup[group], group);
            var batch = new ReplayBatch(key);
            batch.Entries.AddRange(groups[group].Entries);
            batch.Dependencies.UnionWith(dependencies[group]
                .Select(parent => new ReplayBatchKey(rankByGroup[parent], parent)));
            groupBatches[group] = batch;
        }

        batches = ordered.Select(group => groupBatches[group]).ToArray();
        return true;
    }

    private static void AddFileEntries(
        string file,
        IReadOnlyList<TraceEntry> entries,
        TraceReplayProfile profile,
        Dictionary<UploadGroupKey, (HashSet<string> Files, List<TraceEntry> Entries)> groups,
        Dictionary<string, HashSet<UploadGroupKey>> fileGroups)
    {
        var serviceName = (entries.Count > 0 ? entries[0].TryGetServiceName() : null)
            ?? FallbackServiceName;

        if (entries.Count == 0)
        {
            AddGroup(file, new UploadGroupKey(serviceName, DataBoundaryRouting.Unknown, profile), [], groups, fileGroups);
            return;
        }

        foreach (var boundaryGroup in entries.GroupBy(e => (
                     Boundary: DataBoundaryRouting.Normalize(e.DataBoundary), Outcome: e.DataBoundaryOutcome)))
        {
            AddGroup(
                file,
                new UploadGroupKey(serviceName, boundaryGroup.Key.Boundary, profile, boundaryGroup.Key.Outcome),
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

    private static bool ValidateLayerEntries(IReadOnlyList<TraceEntry> entries, TelemetryLayer layer)
    {
        if (entries.Count == 0)
            return false;

        var boundary = entries[0].DataBoundary;
        var outcome = entries[0].DataBoundaryOutcome;
        if (boundary is null || boundary != DataBoundaryRouting.Normalize(boundary))
            return false;

        return entries.All(entry =>
            entry.TryGetServiceName() == layer.ServiceName &&
            entry.DataBoundary == boundary &&
            entry.DataBoundaryOutcome == outcome &&
            (outcome is null || DataBoundaryRouting.ParseOutcome(outcome) is not null) &&
            ReadStringTag(entry, TelemetryTags.DataBoundaryOutcome) == outcome &&
            ReadStringTag(entry, TelemetryLayerTags.Schema) == layer.Schema &&
            ReadStringTag(entry, TelemetryLayerTags.ServiceName) == layer.ServiceName &&
            ReadStringTag(entry, TelemetryTags.DataBoundary) == boundary &&
            Guid.TryParseExact(ReadStringTag(entry, TelemetryLayerTags.InvocationId), "N", out _) &&
            IsHexId(entry.TraceId, 32) &&
            IsHexId(entry.SpanId, 16) &&
            (string.IsNullOrEmpty(entry.ParentSpanId) || entry.ParentSpanId == "0000000000000000" ||
             IsHexId(entry.ParentSpanId, 16)) &&
            !string.IsNullOrEmpty(entry.OperationName) &&
            layer.NameFilter.IsAllowed(entry.OperationName) &&
            Enum.TryParse<ActivityKind>(entry.Kind, out var kind) && Enum.IsDefined(kind) &&
            double.IsFinite(entry.Duration) && entry.Duration >= 0 &&
            DateTimeOffset.TryParse(entry.StartTime, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out _));
    }

    private static bool IsHexId(string? value, int length) =>
        value is not null && value.Length == length &&
        value.Any(character => character != '0') && value.All(char.IsAsciiHexDigit);

    private static string? ReadStringTag(TraceEntry entry, string name)
    {
        if (entry.Tags.ValueKind != JsonValueKind.Array)
            return null;

        string? result = null;
        foreach (var tag in entry.Tags.EnumerateArray())
        {
            if (tag.ValueKind != JsonValueKind.Object)
                return null;
            if (!tag.TryGetProperty("Key", out var key) || key.ValueKind != JsonValueKind.String ||
                key.GetString() != name)
                continue;
            if (result is not null ||
                !tag.TryGetProperty("Value", out var value) || value.ValueKind != JsonValueKind.String)
                return null;
            result = value.GetString();
        }
        return result;
    }

    private readonly record struct UploadGroupKey(
        string ServiceName, string DataBoundary, TraceReplayProfile Profile, string? DataBoundaryOutcome = null);

    private readonly record struct ReplaySpanKey(string TraceId, string SpanId, string DataBoundary, string? DataBoundaryOutcome);

    private readonly record struct ReplayBatchKey(int Rank, UploadGroupKey Group);

    private sealed class ReplayNode(UploadGroupKey group, TraceEntry entry)
    {
        internal UploadGroupKey Group { get; } = group;
        internal TraceEntry Entry { get; } = entry;
        internal ReplaySpanKey? Identity => !string.IsNullOrEmpty(Entry.TraceId) &&
            !string.IsNullOrEmpty(Entry.SpanId)
                ? new ReplaySpanKey(Entry.TraceId, Entry.SpanId, Group.DataBoundary, Group.DataBoundaryOutcome)
                : null;
        internal ReplaySpanKey? ParentIdentity => !string.IsNullOrEmpty(Entry.TraceId) &&
            !string.IsNullOrEmpty(Entry.ParentSpanId) &&
            Entry.ParentSpanId != "0000000000000000"
                ? new ReplaySpanKey(Entry.TraceId, Entry.ParentSpanId, Group.DataBoundary, Group.DataBoundaryOutcome)
                : null;
        internal int? ParentIndex { get; set; }
        internal int Rank { get; set; }
    }

    private sealed class ReplayBatch(ReplayBatchKey key)
    {
        internal ReplayBatchKey Key { get; } = key;
        internal List<TraceEntry> Entries { get; } = [];
        internal HashSet<ReplayBatchKey> Dependencies { get; } = [];
    }
}
