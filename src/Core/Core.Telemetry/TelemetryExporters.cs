// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// The single decision point that maps a resolved <see cref="TelemetryTarget"/> to
/// the trace exporter added to a <see cref="TracerProviderBuilder"/>.
/// </summary>
internal static class TelemetryExporters
{
    internal static void AddTraceExporter(TracerProviderBuilder builder, TelemetryTarget target)
    {
        if (target == TelemetryTarget.Local)
        {
            builder.AddOtlpExporter(options => options.Endpoint = LocalOtlpEndpointResolver.Resolve());
        }
        else if (target == TelemetryTarget.Remote)
        {
            builder.AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = WellKnownEndpoints.AzureMonitorConnectionString;
            });
            builder.SetSampler(new AlwaysOnSampler());
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }
}
