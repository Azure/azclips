// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Cli;

/// <summary>
/// Accumulates per-extension load records during a single AzClips CLI invocation and
/// emits an aggregated timing table on shutdown. Records are isolated per async flow
/// via <see cref="AsyncLocal{T}"/>, matching the pattern used by
/// <see cref="Core.Runtime.ScopedLoggingService"/>.
/// </summary>
/// <remarks>
/// Instrumented only from CLI extension loaders (<see cref="BuiltinExtensionLoader"/> and
/// <see cref="MultiVersionExtensionLoader"/>) which probe the filesystem. PS cmdlets
/// use <c>DefaultAssemblyLoadingProvider</c> which resolves against already-loaded
/// assemblies — there's nothing to time or aggregate on that surface.
/// </remarks>
public sealed class ExtensionLoadTracker
{
    private static readonly AsyncLocal<ExtensionLoadTracker?> _current = new();

    // Aligned columns: extension name (30), load time ms (10), version (12), path (rest).
    private const string HeaderFormat = "[ExtensionLoadTracker] {0,-30} {1,10} {2,-12} {3}";
    private const string RowFormat = "[ExtensionLoadTracker] {0,-30} {1,10:F1} {2,-12} {3}";

    private readonly List<Row> _rows = new();
    private readonly Lock _lock = new();

    /// <summary>
    /// Records a successful extension load in the tracker for the current async flow.
    /// Lazy: the flow-local tracker is instantiated on first use.
    /// </summary>
    public static void Record(string extensionName, double elapsedMs, string version, string path)
    {
        var tracker = _current.Value ??= new ExtensionLoadTracker();
        lock (tracker._lock)
        {
            tracker._rows.Add(new Row(extensionName, elapsedMs, version, path));
        }
    }

    /// <summary>
    /// Emits a Debug-level table of all extension loads recorded on the current async flow,
    /// then clears the tracker. Idempotent: safe to call at shutdown even if nothing was
    /// recorded (no output in that case).
    /// </summary>
    public static void EmitSummary(ILoggingService logger)
    {
        var tracker = _current.Value;
        if (tracker is null)
        {
            return;
        }

        List<Row> snapshot;
        lock (tracker._lock)
        {
            if (tracker._rows.Count == 0)
            {
                return;
            }
            snapshot = new List<Row>(tracker._rows);
            tracker._rows.Clear();
        }

        logger.Debug(string.Format(HeaderFormat, "Extension", "Time (ms)", "Version", "Path"));
        double totalMs = 0;
        foreach (var r in snapshot)
        {
            logger.Debug(string.Format(RowFormat, r.Name, r.ElapsedMs, string.IsNullOrEmpty(r.Version) ? "-" : r.Version, r.Path));
            totalMs += r.ElapsedMs;
        }
        logger.Debug(string.Format(RowFormat, $"Total ({snapshot.Count})", totalMs, "-", "-"));

        _current.Value = null;
    }

    private readonly record struct Row(string Name, double ElapsedMs, string Version, string Path);
}
