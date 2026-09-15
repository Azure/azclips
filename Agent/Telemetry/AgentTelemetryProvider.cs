// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Agent.Telemetry;

public sealed class AgentTelemetryProvider : DefaultTelemetryProvider
{
    public AgentTelemetryProvider(
        IEnumerable<BaseProcessor<Activity>> filterProcessors,
        ITelemetrySender sender,
        IConfigurationService configuration)
    {
        // Must match CLITelemetryProvider's source name: FileExporter writes service.name
        // from it, and TraceUploader relinks parent/child edges only within a group that
        // shares one.
        var sourceName = ClientType.CLI.ToString();
        ActivitySource = new ActivitySource(sourceName);

        if (!configuration.GetConfiguration<TelemetryOptions>().Enabled)
        {
            // Build a default TracerProvider which skips listening to any sources.
            // This is functional because there is an implicit dependency that the entire DI container will be rebuilt
            // on every command invocation
            TracerProvider = TracerProvider.Default;
            return;
        }

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .ConfigureResource(r => r.AddService(ProcessUtils.GetRunningProcess()));

        // Matches CLITelemetryProvider. Without this the agent process emits no span at all for
        // its outbound HTTP — the ARM data boundary lookup included — so a custom span is the only
        // record of a call, with no status code or duration to fall back on. Safe by construction:
        // DefaultTelemetryPropertyFilter admits only method/status/scheme/host/user-agent/length and
        // drops http.url, url.full and http.target, which are the keys that can carry resource paths.
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
