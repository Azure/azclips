// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>Exports through a TracerProvider built for the given telemetry target.</summary>
internal sealed class TracerProviderExportSink : ITraceExportSink
{
    private const int FlushTimeoutMilliseconds = 10_000;

    private readonly TelemetryTarget _target;

    internal TracerProviderExportSink(TelemetryTarget target)
    {
        _target = target;
    }

    public bool TryExport(string serviceName, IReadOnlyList<TraceEntry> entries)
    {
        using var source = new ActivitySource(serviceName);

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(serviceName)
            .ConfigureResource(r => r.AddService(serviceName: serviceName));
        TelemetryExporters.AddTraceExporters(builder, [_target]);

        using var provider = builder.Build()!;

        TraceReplayer.ReplayEntries(source, entries);

        return provider.ForceFlush(FlushTimeoutMilliseconds);
    }
}
