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

            var profile = TraceReplayProfile.Legacy;
            List<TraceEntry> entries;
            if (TraceFileStore.IsLayerFile(file))
            {
                if (!TraceFileStore.TryGetLayerToken(file, out var token) ||
                    !_layers.TryGetValue(token, out var layer))
                {
                    _store.Reject(file, "unknown_layer_file");
                    continue;
                }
                if (!TraceFileFormat.TryParseComplete(content, out entries) ||
                    !ValidateLayerEntries(entries, layer))
                {
                    _store.Reject(file, "invalid_layer_records");
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

        var deliveredGroups = new HashSet<UploadGroupKey>();
        foreach (var (key, group) in groups)
        {
            if (!_sink.TryExport(key.ServiceName, group.Entries, key.DataBoundary, key.Profile))
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
        TraceReplayProfile profile,
        Dictionary<UploadGroupKey, (HashSet<string> Files, List<TraceEntry> Entries)> groups,
        Dictionary<string, HashSet<UploadGroupKey>> fileGroups)
    {
        var serviceName = (entries.Count > 0 ? entries[0].TryGetServiceName() : null)
            ?? FallbackServiceName;

        if (entries.Count == 0)
        {
            AddGroup(file, new UploadGroupKey(serviceName, DataBoundaryRouting.Global, profile), [], groups, fileGroups);
            return;
        }

        foreach (var boundaryGroup in entries.GroupBy(e => DataBoundaryRouting.Normalize(e.DataBoundary)))
        {
            AddGroup(
                file,
                new UploadGroupKey(serviceName, boundaryGroup.Key, profile),
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
        if (boundary is not (DataBoundaryRouting.Eu or DataBoundaryRouting.Global))
            return false;

        return entries.All(entry =>
            entry.TryGetServiceName() == layer.ServiceName &&
            entry.DataBoundary == boundary &&
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
        string ServiceName, string DataBoundary, TraceReplayProfile Profile);
}
