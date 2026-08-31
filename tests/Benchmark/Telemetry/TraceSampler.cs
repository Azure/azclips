// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Core.Telemetry;

namespace Benchmark.Telemetry;

/// <summary>
/// Reduces a benchmark's measured trace files to a fixed-size, evenly spread sample and writes
/// them as JSON Lines. Parses through <see cref="TraceFileFormat.Parse"/>, the same reader the
/// production uploader uses.
/// </summary>
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
    /// Groups spans by TraceId — one group per CLI invocation — ordered by earliest span. File
    /// names carry no sequence; StartTime is ISO-8601 at 100ns resolution, which recovers
    /// invocation order.
    /// </summary>
    private static List<List<TraceEntry>> ReadTracesInOrder(string directory)
    {
        var groups = new Dictionary<string, List<TraceEntry>>(StringComparer.Ordinal);

        // TraceFileStore owns this naming; FileExporter writes through the same type.
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
    /// Takes every stride-th trace, spreading the sample across the whole measured run. The stride
    /// adapts to the trace count: 5 traces against a target of 100 gives a stride of 1 and keeps
    /// all 5.
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
