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

namespace Core.Cli.Telemetry;

public class CLITelemetryProvider : DefaultTelemetryProvider
{
    // The Enabled check no longer lives here — TelemetryService gates every
    // Start*Activity call at runtime so telemetry.enabled changes take effect on
    // the next command without requiring a fresh process. See issue #470.
    public CLITelemetryProvider(IEnumerable<BaseProcessor<Activity>> filterProcessors)
    {
        FileTelemetrySender sender = new();
        ActivitySource = new ActivitySource(ClientType.CLI.ToString());
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(ClientType.CLI.ToString())
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

        sender.ConfigureExporter(builder, FileTelemetrySender.GetTraceDirectory());
        TracerProvider = builder.Build();
    }
}
