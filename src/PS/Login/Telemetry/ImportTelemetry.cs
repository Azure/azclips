// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;

namespace Microsoft.Azure.Azclips.Cmdlets.Login.Telemetry;

/// <summary>
/// Emits the module import trace.
///
/// Import cannot be instrumented directly: emitting a span requires ITelemetryService,
/// whose construction costs more than twice the import it would be measuring, so doing
/// it inline would roughly quadruple import time and report a figure inflated by its own
/// instrumentation. Instead OnImport records timestamps — effectively free — and hands
/// the emit to a background thread, which also pre-warms the telemetry stack so the
/// first cmdlet no longer pays for it.
/// </summary>
internal static class ImportTelemetry
{
    internal const string PerformanceSpanName = "PowerShell.Performance";
    internal const string ContainerSpanName = "PowerShell.Performance.Container";

    internal const string Cold = "cold";
    internal const string Warm = "warm";

    private static int _importCount;

    /// <summary>
    /// Returns <see cref="Cold"/> for the first import in this process and
    /// <see cref="Warm"/> thereafter. PowerShell does not unload the module assembly on
    /// Remove-Module, so this static survives a remove/re-import cycle — which is exactly
    /// what makes the distinction meaningful.
    /// </summary>
    internal static string NextImportKind() =>
        Interlocked.Increment(ref _importCount) == 1 ? Cold : Warm;

    /// <summary>
    /// Materialises the recorded timeline as its own trace. Synchronous and free of
    /// ambient state so it can be tested directly.
    /// </summary>
    /// <param name="importEnd">
    /// When import actually finished. Required because this runs after the fact: letting
    /// the root close on dispose would stretch its duration to emit time.
    /// </param>
    internal static void Emit(
        ITelemetryService telemetry,
        PerformanceTimeline timeline,
        string importKind,
        DateTime importEnd)
    {
        // Its own trace, not a child of any cmdlet: import precedes every cmdlet, so
        // attaching backdated phases to a cmdlet root would put a child before its parent.
        using var root = telemetry.StartRootActivity(timeline.StartedAt);
        root?.SetTag(TelemetryTags.ImportKind, importKind);
        timeline.Replay(telemetry);
        root?.SetEndTime(importEnd);
    }

    /// <summary>
    /// Runs <see cref="Emit"/> off the import path. Best effort: nothing is emitted if the
    /// process exits before the thread is scheduled, which is accepted because such a
    /// session runs no cmdlets and so produces no telemetry either way.
    /// </summary>
    internal static void EmitInBackground(PerformanceTimeline timeline, string importKind, DateTime importEnd)
    {
        _ = Task.Run(() =>
        {
            try
            {
                // Check the opt-out before resolving ITelemetryService: resolving it builds
                // the exporter, which a user who disabled telemetry should never get.
                var configuration = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
                if (!configuration.GetConfiguration<TelemetryOptions>().Enabled)
                {
                    return;
                }

                Emit(AzclipsServiceProvider.GetRequiredService<ITelemetryService>(), timeline, importKind, importEnd);
            }
            catch
            {
                // Best effort. Telemetry must never break module import.
            }
        });
    }
}
