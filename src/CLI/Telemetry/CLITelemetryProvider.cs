// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Cli.Telemetry;

public class CLITelemetryProvider : ITelemetryProvider
{
    public ActivitySource ActivitySource { get; }
    public TracerProvider TracerProvider { get; }

    public CLITelemetryProvider()
    {
        FileTelemetrySender sender = new();
        ActivitySource = new ActivitySource(ClientType.CLI.ToString());
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(ClientType.CLI.ToString());

        builder.AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequestMessage = (activity, request) =>
            {
                activity.SetTag("http.request.header.user-agent", request.Headers.UserAgent?.ToString());
            };
        });

        sender.ConfigureExporter(builder, FileTelemetrySender.GetTraceDirectory());
        TracerProvider = builder.Build()!;
    }
}
