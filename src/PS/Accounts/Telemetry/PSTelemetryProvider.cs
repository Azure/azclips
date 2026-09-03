// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts.Telemetry;

public class PSTelemetryProvider : DefaultTelemetryProvider
{
    // The Enabled check no longer lives here — TelemetryService gates every
    // Start*Activity call at runtime so telemetry.enabled changes take effect on
    // the next cmdlet without requiring a module reload.
    public PSTelemetryProvider(
        IEnumerable<BaseProcessor<Activity>> filterProcessors,
        ITelemetrySender sender)
    {
        ActivitySource = new ActivitySource(ClientType.PowerShell.ToString());
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(ClientType.PowerShell.ToString())
            .ConfigureResource(r => r.AddService(ProcessUtils.GetRunningProcess()));

        builder.AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequestMessage = (activity, request) =>
            {
                activity.SetTag("http.request.header.user-agent", request.Headers.UserAgent?.ToString());
            };
        });

        foreach (var processor in filterProcessors)
        {
            builder.AddProcessor(processor);
        }

        sender.ConfigureExporter(builder);
        TracerProvider = builder.Build();
    }
}
