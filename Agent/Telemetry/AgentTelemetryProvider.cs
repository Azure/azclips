// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Agent.Telemetry;

/// <summary>
/// Telemetry pipeline for azclips-agent.exe. Mirrors
/// <c>Core.Cli.Telemetry.CLITelemetryProvider</c> rather than sharing it, because
/// Core.Agent does not reference Core.Cli.
/// </summary>
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

        // Agent spans come from AgentTracing.Source, a plain ActivitySource that does not
        // consult telemetry.enabled. Skipping listener construction is what makes the
        // opt-out cover them all, tool.* included. Once per process is enough — the agent
        // is short-lived and single-shot.
        if (!configuration.GetConfiguration<TelemetryOptions>().Enabled)
        {
            TracerProvider = TracerProvider.Default;
            return;
        }

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .ConfigureResource(r => r.AddService(ProcessUtils.GetRunningProcess()));

        foreach (var processor in filterProcessors)
        {
            builder.AddProcessor(processor);
        }

        sender.ConfigureExporter(builder);
        TracerProvider = builder.Build();
    }
}
