// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>Exports through one TracerProvider per configured telemetry target.</summary>
internal sealed class TracerProviderExportSink : ITraceExportSink
{
    private const int FlushTimeoutMilliseconds = 10_000;

    private readonly IReadOnlyList<TelemetryTarget> _targets;

    internal TracerProviderExportSink(IReadOnlyList<TelemetryTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
            throw new ArgumentException("At least one target is required.", nameof(targets));

        _targets = targets;
    }

    /// <summary>
    /// Picks the target whose verdict decides whether a trace file is deleted. Remote
    /// outranks Local, so a failing local collector never blocks deletion.
    /// </summary>
    internal static TelemetryTarget SelectAuthoritative(IReadOnlyList<TelemetryTarget> targets) =>
        targets.Contains(TelemetryTarget.Remote) ? TelemetryTarget.Remote : TelemetryTarget.Local;

    public bool TryExport(string serviceName, IReadOnlyList<TraceEntry> entries) =>
        ExportToTargets(_targets, target => ExportTo(target, serviceName, entries));

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

    private static bool ExportTo(
        TelemetryTarget target,
        string serviceName,
        IReadOnlyList<TraceEntry> entries)
    {
        using var source = new ActivitySource(serviceName);

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(serviceName)
            .ConfigureResource(r => r.AddService(serviceName: serviceName));
        TelemetryExporters.AddTraceExporters(builder, [target]);

        using var provider = builder.Build()!;

        TraceReplayer.ReplayEntries(source, entries);

        return provider.ForceFlush(FlushTimeoutMilliseconds);
    }
}
