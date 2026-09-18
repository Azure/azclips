// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Azure.Monitor.OpenTelemetry.Exporter;
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
        string? dataBoundary = null)
    {
        foreach (var target in targets)
        {
            AddTraceExporter(builder, target);
        }

        // Route Aria telemetry to the EU or Global collector based on the active
        // profile's ARM data boundary. EU tenants → EU-compliant endpoint; all
        // others (including unknown/missing) → Global endpoint.
        if (targets.Contains(TelemetryTarget.Remote))
        {
            var (connectionString, endpoint) = ResolveAriaExporter(dataBoundary);
            builder.AddProcessor(new SimpleActivityExportProcessor(
                new AriaActivityExporter(connectionString, endpoint)));
        }

        builder.SetSampler(new AlwaysOnSampler());
    }

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
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }
    }

    internal static (string ConnectionString, Uri? Endpoint) ResolveAriaExporter(string? dataBoundary = null)
    {
        var effectiveBoundary = dataBoundary ?? TelemetryRoutingContext.DataBoundary;
        var isEu = effectiveBoundary is null
            ? DataBoundaryResolver.IsEuDataBoundary()
            : DataBoundaryRouting.IsEu(effectiveBoundary);

        return isEu
            ? (WellKnownEndpoints.AriaEuConnectionString, WellKnownEndpoints.AriaEuEndpoint)
            : (WellKnownEndpoints.AriaConnectionString, null);
    }
}

