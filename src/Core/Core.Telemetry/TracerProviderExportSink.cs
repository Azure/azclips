// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>Exports through one TracerProvider per configured telemetry target.</summary>
internal sealed class TracerProviderExportSink : ITraceExportSink
{
    private const int FlushTimeoutMilliseconds = 10_000;

    private readonly IReadOnlyList<TelemetryTarget> _targets;
    private readonly Action<TracerProviderBuilder, TelemetryTarget, UploadRegion?>? _configureExporter;

    internal TracerProviderExportSink(
        IReadOnlyList<TelemetryTarget> targets,
        Action<TracerProviderBuilder, TelemetryTarget, UploadRegion?>? configureExporter = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
            throw new ArgumentException("At least one target is required.", nameof(targets));

        _targets = targets;
        _configureExporter = configureExporter;
    }

    /// <summary>
    /// Picks the target whose verdict decides whether a trace file is deleted. Remote
    /// outranks Local, so a failing local collector never blocks deletion.
    /// </summary>
    internal static TelemetryTarget SelectAuthoritative(IReadOnlyList<TelemetryTarget> targets) =>
        targets.Contains(TelemetryTarget.Remote) ? TelemetryTarget.Remote : TelemetryTarget.Local;

    public bool TryExport(
        string serviceName,
        IReadOnlyList<TraceEntry> entries,
        UploadRegion? uploadRegion = null,
        TraceReplayProfile profile = TraceReplayProfile.Legacy,
        TraceReplayContext? replayContext = null) =>
        ExportToTargets(_targets,
            target => ExportTo(target, serviceName, entries, uploadRegion, profile, replayContext?.ForTarget(target)));

    /// <summary>
    /// Attempts every target and returns the authoritative target's verdict.
    /// </summary>
    internal static bool ExportToTargets(IReadOnlyList<TelemetryTarget> targets, Func<TelemetryTarget, bool> exportTo)
    {
        var authoritative = SelectAuthoritative(targets);
        var delivered = true;

        foreach (var target in targets)
        {
            var exported = exportTo(target);
            if (target == authoritative)
            {
                delivered = exported;
            }
        }

        return delivered;
    }

    private bool ExportTo(
        TelemetryTarget target,
        string serviceName,
        IReadOnlyList<TraceEntry> entries,
        UploadRegion? uploadRegion,
        TraceReplayProfile profile,
        Dictionary<(string? TraceId, string? SpanId, UploadRegion UploadRegion), ActivitySpanId>? replayMap)
    {
        using var source = new ActivitySource(serviceName);

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(serviceName)
            .ConfigureResource(r => r.AddService(serviceName: serviceName));
        if (_configureExporter is not null)
            _configureExporter(builder, target, uploadRegion);
        else
            TelemetryExporters.AddTraceExporters(builder, [target], uploadRegion);

        using var provider = builder.Build()!;

        TraceReplayer.ReplayEntries(source, entries, profile, replayMap);

        return provider.ForceFlush(FlushTimeoutMilliseconds);
    }
}
