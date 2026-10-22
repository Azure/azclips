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
    // TelemetryService.Start*Activity checks telemetry.enabled for the CLI's own spans. Agent
    // tools that run in this process (for example `az agent tool resource find`) start their
    // tool.* spans on a source with the same name directly, which skips that check, so the
    // opt-out is enforced by not listening at all, as AgentTelemetryProvider does. The CLI builds
    // a new container for every command, so reading the setting once here is enough.
    public CLITelemetryProvider(
        IEnumerable<BaseProcessor<Activity>> filterProcessors,
        ITelemetrySender sender,
        IConfigurationService configuration)
    {
        ActivitySource = new ActivitySource(ClientType.CLI.ToString());

        if (!IsTelemetryEnabled(configuration))
        {
            TracerProvider = TracerProvider.Default;
            return;
        }

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

    private static bool IsTelemetryEnabled(IConfigurationService configuration)
    {
        try
        {
            return configuration.GetConfiguration<TelemetryOptions>().Enabled;
        }
        catch (Exception)
        {
            // The container resolves this before Main's error handling. An unreadable configuration
            // fails the command there with a clear message; record nothing in the meantime.
            return false;
        }
    }
}
