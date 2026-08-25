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
    // Kept for parity with PSTelemetryProvider / TUITelemetryProvider. The
    // TelemetryService.Start*Activity gate is the single source of truth for
    // whether spans are recorded; the CLI process is single-shot so the runtime
    // toggle is largely a no-op here, but the shared pattern keeps the three
    // providers uniform.
    public CLITelemetryProvider(
        IEnumerable<BaseProcessor<Activity>> filterProcessors,
        ITelemetrySender sender)
    {
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

        sender.ConfigureExporter(builder);
        TracerProvider = builder.Build();
    }
}
