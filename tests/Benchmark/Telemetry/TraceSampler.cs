// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Core.Telemetry;

namespace Benchmark.Telemetry;

/// <summary>
/// Reduces a benchmark's measured trace files to a fixed-size, evenly spread sample and writes them
/// as JSON Lines.
/// </summary>
/// <remarks>
/// Every iteration emits telemetry so the cost stays uniform across the run — sampling only decides
/// what survives on disk, and it happens after the measurements are taken. Parsing goes through
/// <see cref="TraceFileFormat.Parse"/> so benchmark files are read exactly as the
/// production uploader reads them.
/// </remarks>
internal static class TraceSampler
{
    private const int TargetSampleCount = 100;

    /// <summary>
    /// Writes up to <see cref="TargetSampleCount"/> traces from <paramref name="actualDirectory"/>
    /// to <paramref name="outputPath"/>, one span per line, grouped by trace and in invocation
    /// order. Returns the number of traces written.
    /// </summary>
    internal static int Prune(string actualDirectory, string outputPath)
    {
        if (!Directory.Exists(actualDirectory))
        {
            return 0;
        }

        var sampled = Sample(ReadTracesInOrder(actualDirectory), TargetSampleCount);

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var writer = new StreamWriter(outputPath, append: false);
        foreach (var trace in sampled)
        {
            foreach (var span in trace)
            {
                writer.WriteLine(JsonSerializer.Serialize(span));
            }
        }

        return sampled.Count;
    }

    /// <summary>
    /// Groups every span by TraceId — each group is one CLI invocation — and orders the groups by
    /// their earliest span. File names carry no sequence, but StartTime is ISO-8601 with 100ns
    /// resolution, which recovers exact invocation order.
    /// </summary>
    private static List<List<TraceEntry>> ReadTracesInOrder(string directory)
    {
        var groups = new Dictionary<string, List<TraceEntry>>(StringComparer.Ordinal);

        // Pattern comes from TraceFileStore so the reader cannot drift from the writer:
        // FileExporter names files through the same type.
        foreach (var file in Directory.EnumerateFiles(directory, TraceFileStore.PendingFilePattern))
        {
            foreach (var entry in TraceFileFormat.Parse(File.ReadAllText(file)))
            {
                var key = entry.TraceId ?? string.Empty;
                if (!groups.TryGetValue(key, out var spans))
                {
                    spans = new List<TraceEntry>();
                    groups[key] = spans;
                }

                spans.Add(entry);
            }
        }

        return groups.Values.OrderBy(EarliestStart).ToList();
    }

    private static DateTime EarliestStart(List<TraceEntry> spans)
    {
        var earliest = DateTime.MaxValue;
        foreach (var span in spans)
        {
            if (DateTime.TryParse(
                    span.StartTime,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed)
                && parsed < earliest)
            {
                earliest = parsed;
            }
        }

        return earliest;
    }

    /// <summary>
    /// Takes every stride-th trace so the sample is spread evenly across the whole measured run
    /// rather than clustered at the start. The stride adapts to the trace count: a 5-iteration
    /// smoke run yields a stride of 1 and keeps all 5 traces rather than emitting nothing.
    /// </summary>
    private static List<List<TraceEntry>> Sample(
        List<List<TraceEntry>> traces,
        int target)
    {
        var sampled = new List<List<TraceEntry>>();
        if (traces.Count == 0 || target <= 0)
        {
            return sampled;
        }

        var stride = Math.Max(1, traces.Count / target);
        for (var i = 0; i < traces.Count && sampled.Count < target; i += stride)
        {
            sampled.Add(traces[i]);
        }

        return sampled;
    }
}
