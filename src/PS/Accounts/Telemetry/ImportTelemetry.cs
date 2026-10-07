// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Runtime;
using Core.Telemetry;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts.Telemetry;

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
    internal const string PerformanceSpanName = "PowerShell.Startup";
    internal const string ContainerSpanName = "PowerShell.Startup.Container";

    internal const string Cold = "cold";
    internal const string Warm = "warm";

    private static int _importCount;

    /// <summary>
    /// Maps a one-based import count to its kind. Separated from <see cref="NextImportKind"/>
    /// so the mapping can be asserted directly: the counter behind it is process-wide and
    /// cannot be reset, which would otherwise make any test of it order-dependent.
    /// </summary>
    internal static string ImportKindFor(int importCount) => importCount == 1 ? Cold : Warm;

    /// <summary>
    /// Returns <see cref="Cold"/> for the first import in this process and
    /// <see cref="Warm"/> thereafter. PowerShell does not unload the module assembly on
    /// Remove-Module, so this static survives a remove/re-import cycle — which is exactly
    /// what makes the distinction meaningful.
    /// </summary>
    internal static string NextImportKind() => ImportKindFor(Interlocked.Increment(ref _importCount));

    /// <summary>
    /// Materialises the recorded timeline as its own trace. Synchronous and free of
    /// ambient state so it can be tested directly.
    /// </summary>
    /// <param name="importEnd">
    /// When import actually finished. Required because this runs after the fact: letting
    /// the root close on dispose would stretch its duration to emit time.
    /// </param>
    /// <param name="tagRoot">
    /// Optional callback invoked on the root immediately after creation, before
    /// <paramref name="timeline"/> is replayed. MUST run before the replay (not after):
    /// FileExporter/AriaActivityExporter export each replayed phase span synchronously as
    /// it's disposed, so a tag applied only after Replay returns would never reach them —
    /// see EmitInBackground, which uses this to tag TenantId/DataBoundary from the active
    /// profile so import/startup spans route to the correct Aria endpoint instead of
    /// silently falling back to Global.
    /// </param>
    internal static void Emit(
        ITelemetryService telemetry,
        PerformanceTimeline timeline,
        string importKind,
        DateTime importEnd,
        Action<Activity?>? tagRoot = null)
    {
        // Its own trace, not a child of any cmdlet: import precedes every cmdlet, so
        // attaching backdated phases to a cmdlet root would put a child before its parent.
        using var root = telemetry.StartRootActivity(timeline.StartedAt);
        root?.SetTag(TelemetryTags.ImportKind, importKind);
        tagRoot?.Invoke(root);
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
        _ = Task.Run(async () =>
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

                var configuredLogLevel = configuration.GetConfiguration<CoreOptions>().Logging.Level;
                using var loggingScope = ScopedLoggingService.UseScope(
                    new LevelFilteredLoggingService(new ConsoleLoggingService(), configuredLogLevel));

                // Best-effort: resolves the CURRENTLY ACTIVE profile (module import precedes
                // any command, so there is no --profile override to bind against yet). Runs
                // BEFORE Emit so the tag is on the root before Replay creates the phase spans.
                var (tenantId, boundary, subscriptionId, authType) = await TelemetryRoutingResolver.ResolveAsync(
                    AzclipsServiceProvider.GetRequiredService<IProfileService>(),
                    AzclipsServiceProvider.GetRequiredService<IAuthService>(),
                    profileNameOverride: null,
                    CancellationToken.None);

                Emit(
                    AzclipsServiceProvider.GetRequiredService<ITelemetryService>(),
                    timeline,
                    importKind,
                    importEnd,
                    root =>
                    {
                        if (tenantId is not null) root?.SetTag(TelemetryTags.TenantId, tenantId);
                        if (boundary is not null) root?.SetTag(TelemetryTags.DataBoundary, boundary);
                        if (subscriptionId is not null) root?.SetTag(TelemetryTags.SubscriptionId, subscriptionId);
                        if (authType is not null) root?.SetTag(TelemetryTags.AuthType, authType);
                    });
            }
            catch
            {
                // Best effort. Telemetry must never break module import.
            }
        });
    }
}
