// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Cli;

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
    // Aligned columns: extension name (30), load time ms (10), version (12), path (rest).
    private const string HeaderFormat = "[ExtensionLoadTracker] {0,-30} {1,10} {2,-12} {3}";
    private const string RowFormat = "[ExtensionLoadTracker] {0,-30} {1,10:F1} {2,-12} {3}";

    private static readonly List<Row> _rows = new();
    private static readonly Lock _lock = new();

    /// <summary>Records a successful extension load.</summary>
    public static void Record(string extensionName, double elapsedMs, string version, string path)
    {
        lock (_lock)
        {
            _rows.Add(new Row(extensionName, elapsedMs, version, path));
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
    /// Routing through the streams — not <see cref="ILoggingService.Debug"/> directly — is
    /// required because <c>EmitSummary</c> is invoked in <c>Program.Main</c>'s <c>finally</c>
    /// block, after the session's <c>UseScope</c> has been disposed, so
    /// <see cref="ScopedLoggingService"/> would otherwise fall back to the ungated
    /// <see cref="ConsoleLoggingService"/> and leak the table under <c>warn</c>/<c>error</c>.
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

        writeDebug(string.Format(HeaderFormat, "Extension", "Time (ms)", "Version", "Path"));
        double totalMs = 0;
        foreach (var r in snapshot)
        {
            writeDebug(string.Format(RowFormat, r.Name, r.ElapsedMs, string.IsNullOrEmpty(r.Version) ? "-" : r.Version, r.Path));
            totalMs += r.ElapsedMs;
        }
        writeDebug(string.Format(RowFormat, $"Total ({snapshot.Count})", totalMs, "-", "-"));
    }

    private readonly record struct Row(string Name, double ElapsedMs, string Version, string Path);
}
