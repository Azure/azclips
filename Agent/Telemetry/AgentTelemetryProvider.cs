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

        // No AddHttpClientInstrumentation here, deliberately. The CLI, TUI and PS providers call it,
        // but DefaultTelemetryActivityNameFilter rejects "System.Net.Http.HttpRequestOut" — asserted
        // in ActivityNameFilterTests — so every span it produces is dropped before export. Adding it
        // would buy instrumentation overhead on every agent HTTP call and no data. Outbound calls
        // that need timing carry it on their own allowlisted span instead.
        foreach (var processor in filterProcessors)
        {
            builder.AddProcessor(processor);
        }

        sender.ConfigureExporter(builder);
        TracerProvider = builder.Build();
    }
}
