// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;

namespace Core.Cli.Agent;

/// <summary>
/// Owns the agent session's JSONL trace file on the PARENT (`az`) process side.
///
/// The parent decides whether tracing is on (agent.trace.enabled), picks the file
/// path, and writes the first record: the exception that triggered the fallback —
/// captured with its real type here, before it's flattened to a message string on
/// the wire. The path is then handed to the spawned azclips-agent via
/// <c>--trace-file</c>, and the child APPENDS its session trace to the same file,
/// so one file holds the whole timeline (parent exception → child session) in
/// order.
///
/// Best-effort: any failure here is swallowed — telemetry must never break the
/// fallback path.
/// </summary>
internal static class AgentFallbackTrace
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// If tracing is enabled, create the trace file, write the triggering
    /// exception as the first record, and return the file path (to forward to the
    /// child via <c>--trace-file</c>). Returns null when tracing is off or on any
    /// failure — the caller simply doesn't pass <c>--trace-file</c> in that case.
    /// </summary>
    /// <param name="trace">The resolved agent.trace options.</param>
    /// <param name="ex">The exception that triggered the fallback.</param>
    /// <param name="source">Which catch produced it (e.g. "HttpRequestException", "Exception").</param>
    /// <param name="excluded">Whether the exception was excluded from the fallback.</param>
    /// <param name="willSpawn">Whether the agent subprocess will actually be spawned.</param>
    public static string? BeginSession(
        TraceOptions trace,
        Exception ex,
        string source,
        bool excluded,
        bool willSpawn)
    {
        if (trace is null || !trace.Enabled || string.IsNullOrWhiteSpace(trace.Path))
            return null;

        try
        {
            var traceDir = ExpandHome(trace.Path);
            if (!string.IsNullOrEmpty(traceDir))
                Directory.CreateDirectory(traceDir);

            var fileName = $"trace-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.jsonl";
            var fullPath = Path.GetFullPath(Path.Combine(traceDir, fileName));

            // Write the triggering exception as the first line, then close the
            // stream. The child opens the same path in append mode, so there is
            // no concurrent writer — the parent is done before the child starts.
            using (var writer = new StreamWriter(
                new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read)))
            {
                var record = new
                {
                    seq = 0,
                    ts = DateTime.UtcNow.ToString("o"),
                    kind = "fallback_exception",
                    data = new
                    {
                        exceptionType = ex.GetType().FullName,
                        message = ex.Message,
                        source,
                        excluded,
                        willSpawn,
                    },
                };
                writer.WriteLine(JsonSerializer.Serialize(record, JsonOpts));
            }

            return fullPath;
        }
        catch
        {
            // Diagnostics-only: never let a trace failure break the fallback.
            return null;
        }
    }

    // .NET does not expand a leading "~"; do it ourselves so the documented default
    // (~/.azclips/trace) resolves under the user profile instead of a literal "~" dir.
    private static string ExpandHome(string path)
    {
        if (path == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        return path;
    }
}
