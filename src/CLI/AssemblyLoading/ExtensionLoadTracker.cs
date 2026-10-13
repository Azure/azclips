// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli;

using System.Globalization;
using Core.Cli.Output;

/// <summary>
/// Accumulates per-extension load records during a single AzClips CLI invocation
/// and emits an aggregated timing table on shutdown.
/// </summary>
/// <remarks>
/// Instrumented only from CLI extension loaders (<see cref="BundledExtensionLoader"/>,
/// <see cref="InstalledExtensionLoader"/>) which probe the filesystem. PS cmdlets use
/// <c>DefaultAssemblyLoadingProvider</c> which resolves against already-loaded assemblies —
/// there's nothing to time or aggregate on that surface, so the tracker stays quiet there.
/// <para>
/// State is stored in plain static fields (not <see cref="AsyncLocal{T}"/>) because the CLI
/// runs a single invocation per process. If we ever host multiple concurrent invocations
/// in the same process, revisit and adopt an explicit <c>BeginScope</c>/AsyncLocal pattern
/// like <c>ScopedLoggingService</c> uses.
/// </para>
/// </remarks>
public static class ExtensionLoadTracker
{
    private static readonly List<Row> _rows = new();
    private static readonly Lock _lock = new();

    /// <summary>Records a successful extension load (defaults to Bundled loader).</summary>
    public static void Record(string extensionName, double elapsedMs, string version, string path)
        => Record(extensionName, "Bundled", elapsedMs, version, path);

    /// <summary>Records a successful extension load with explicit loader/source metadata.</summary>
    public static void Record(string extensionName, string loader, double elapsedMs, string version, string path)
    {
        lock (_lock)
        {
            _rows.Add(new Row(extensionName, loader, elapsedMs, version, path));
        }
    }

    /// <summary>Clears all accumulated rows. Primarily for testing.</summary>
    internal static void Reset()
    {
        lock (_lock)
        {
            _rows.Clear();
        }
    }

    /// <summary>
    /// Emits a Debug-level table of all recorded loads and clears the buffer. Idempotent:
    /// silent no-op when nothing was recorded (e.g. <c>az --version</c> which loads no extensions).
    /// </summary>
    /// <param name="writeDebug">
    /// Callback that writes one Debug line. Callers are expected to pass a delegate that
    /// applies the CLI log-level gate (e.g. <c>streams.WriteDebug</c> built by
    /// <c>Program.BuildStreams</c>), so the summary is suppressed under levels &lt; Debug.
    /// Routing through the streams — not <see cref="ILoggingService.Debug"/> directly — preserves
    /// the CLI session's resolved policy, including <c>--debug</c>, after its <c>UseScope</c> has
    /// been disposed. The no-session fallback only knows the global configured level.
    /// </param>
    public static void EmitSummary(Action<string> writeDebug)
    {
        List<Row> snapshot;
        lock (_lock)
        {
            if (_rows.Count == 0)
            {
                return;
            }
            snapshot = new List<Row>(_rows);
            _rows.Clear();
        }

        var headers = new[] { "Extension", "Loader", "Time (ms)", "Version" };
        var tableRows = new List<string[]>(snapshot.Count + 1);
        double totalMs = 0;
        foreach (var r in snapshot)
        {
            tableRows.Add(new[]
            {
                r.Name,
                r.Loader,
                r.ElapsedMs.ToString("F1", CultureInfo.InvariantCulture),
                string.IsNullOrEmpty(r.Version) ? "-" : r.Version
            });
            totalMs += r.ElapsedMs;
        }

        tableRows.Add(new[]
        {
            $"Total ({snapshot.Count})",
            "-",
            totalMs.ToString("F1", CultureInfo.InvariantCulture),
            "-"
        });

        var table = TableFormatter.FormatRows(tableRows, headers, includeHeaderSeparator: false);
        writeDebug("[ExtensionLoadTracker] Extension load timing for this command:");
        foreach (var line in table.Split('\n'))
        {
            writeDebug($"[ExtensionLoadTracker] {line}");
        }
    }

    private readonly record struct Row(string Name, string Loader, double ElapsedMs, string Version, string Path);
}
