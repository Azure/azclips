// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// The single decision point that maps a resolved <see cref="TelemetryTarget"/> to
/// the trace exporter added to a <see cref="TracerProviderBuilder"/>. Future signals
/// (metrics, logs) add sibling methods here so the "given target → exporter" choice
/// lives in exactly one place.
/// </summary>
internal static class TelemetryExporters
{
    internal static void AddTraceExporter(TracerProviderBuilder builder, TelemetryTarget target)
    {
        if (target == TelemetryTarget.Local)
        {
            builder.AddOtlpExporter(options => options.Endpoint = new Uri(WellKnownEndpoints.LocalOtlp));
        }
        else
        {
            builder.AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = WellKnownEndpoints.AzureMonitorConnectionString;
            });
        }
    }
}
