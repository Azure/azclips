// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Azure.Monitor.OpenTelemetry.Exporter;
using Core.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry;

/// <summary>
/// Maps resolved <see cref="TelemetryTarget"/> values to the trace exporters added to a
/// <see cref="TracerProviderBuilder"/>.
/// </summary>
internal static class TelemetryExporters
{
    internal static void AddTraceExporters(
        TracerProviderBuilder builder,
        IReadOnlyList<TelemetryTarget> targets,
        UploadRegion? uploadRegion = null)
    {
        foreach (var target in targets)
        {
            AddTraceExporter(builder, target);
        }

        if (targets.Contains(TelemetryTarget.Remote))
        {
            // Replay groups have fixed routing metadata; live providers route each Activity.
            AriaActivityExporter exporter;
            if (uploadRegion is null)
            {
                exporter = new AriaActivityExporter();
            }
            else
            {
                var (connectionString, endpoint) = ResolveAriaExporter(uploadRegion.Value);
                exporter = new AriaActivityExporter(connectionString, endpoint);
            }

            builder.AddProcessor(new SimpleActivityExportProcessor(exporter));
        }

        builder.SetSampler(new AlwaysOnSampler());
    }

    internal static void AddTraceExporter(TracerProviderBuilder builder, TelemetryTarget target)
    {
        TelemetryNetworkGuard.EnsureAllowed();
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
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }

    internal static (string ConnectionString, Uri? Endpoint) ResolveAriaExporter(
        UploadRegion uploadRegion)
    {
        return uploadRegion switch
        {
            UploadRegion.ROW => (WellKnownEndpoints.AriaConnectionString, WellKnownEndpoints.AriaRowEndpoint),
            UploadRegion.EU => (WellKnownEndpoints.AriaConnectionString, WellKnownEndpoints.AriaEuEndpoint),
            _ => throw new ArgumentOutOfRangeException(nameof(uploadRegion)),
        };
    }
}
