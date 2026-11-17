// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text;

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

    internal static bool TryParseComplete(string content, out List<TraceEntry> entries)
    {
        entries = [];
        using var reader = new StringReader(content);
        var batch = new StringBuilder();
        var headerSeen = false;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.StartsWith(BatchHeaderPrefix, StringComparison.Ordinal))
            {
                if (!line.EndsWith(" ---", StringComparison.Ordinal) ||
                    (headerSeen && !TryAppendBatch(batch.ToString(), entries)))
                    return false;

                headerSeen = true;
                batch.Clear();
            }
            else if (!headerSeen)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    return false;
            }
            else
            {
                batch.AppendLine(line);
            }
        }

        return headerSeen && TryAppendBatch(batch.ToString(), entries) && entries.Count > 0;
    }

    private static bool TryAppendBatch(string json, List<TraceEntry> entries)
    {
        try
        {
            var batch = JsonSerializer.Deserialize<List<TraceEntry>>(json);
            if (batch is null || batch.Any(entry => entry is null))
                return false;

            entries.AddRange(batch);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
