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

        // Route Aria telemetry to the EU or ROW (Rest of World) regional collector based on
        // the active profile's ARM data boundary. EU tenants → EU-compliant endpoint; all
        // others (including unknown/missing) → explicit ROW endpoint. Both destinations are
        // pinned explicitly rather than leaving either to OneCollector's default/global
        // endpoint, which can route traffic to any regional Collector/AEF instance — including
        // the EU one — causing Geneva Delivery Service (GDS) to misroute ROW data into the EU
        // Kusto cluster.
        if (targets.Contains(TelemetryTarget.Remote))
        {
            // dataBoundary supplied: the caller already knows the destination for this
            // exporter's whole lifetime (buffered replay of an already-partitioned group) —
            // build a fixed-destination exporter. Otherwise build a dynamic-destination
            // exporter, since this exporter may live across many command invocations with
            // different tenants/boundaries (e.g. PowerShell's singleton TracerProvider) — it
            // resolves the boundary per exported Activity from that Activity's own tag.
            AriaActivityExporter exporter;
            if (dataBoundary is null)
            {
                exporter = new AriaActivityExporter();
            }
            else
            {
                var (connectionString, endpoint) = ResolveAriaExporter(dataBoundary);
                exporter = new AriaActivityExporter(connectionString, endpoint);
            }

            builder.AddProcessor(new SimpleActivityExportProcessor(exporter));
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
        var isEu = dataBoundary is null
            ? DataBoundaryResolver.IsEuDataBoundary()
            : DataBoundaryRouting.IsEu(dataBoundary);

        return isEu
            ? (WellKnownEndpoints.AriaEuConnectionString, WellKnownEndpoints.AriaEuEndpoint)
            : (WellKnownEndpoints.AriaConnectionString, WellKnownEndpoints.AriaRowEndpoint);
    }
}

