// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Core.Telemetry;

/// <summary>Reads and writes the trace file format: timestamped JSON batches.</summary>
internal static class TraceFileFormat
{
    private const string BatchHeaderPrefix = "--- Batch at ";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Renders one batch for appending to a trace file.</summary>
    internal static string FormatBatch(IReadOnlyList<object> traces, DateTime timestampUtc)
    {
        var json = JsonSerializer.Serialize(traces, WriteOptions);
        return $"\n{BatchHeaderPrefix}{timestampUtc:O} ---\n{json}\n";
    }

    /// <summary>Parses every batch in a file. Malformed batches are skipped.</summary>
    internal static List<TraceEntry> Parse(string content)
    {
        var allTraces = new List<TraceEntry>();

        foreach (var part in content.Split(BatchHeaderPrefix))
        {
            var jsonStart = part.IndexOf('[');
            if (jsonStart < 0)
                continue;

            var jsonEnd = part.LastIndexOf(']');
            if (jsonEnd < jsonStart)
                continue;

            try
            {
                var traces = JsonSerializer.Deserialize<List<TraceEntry>>(part[jsonStart..(jsonEnd + 1)]);
                if (traces != null)
                    allTraces.AddRange(traces);
            }
            catch (JsonException)
            {
                // Truncated or corrupt batch.
            }
        }

        return allTraces;
    }
}
