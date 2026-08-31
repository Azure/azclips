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
/// Telemetry pipeline for azclips-agent.exe. Deliberately mirrors
/// <c>Core.Cli.Telemetry.CLITelemetryProvider</c> rather than sharing it: Core.Agent does
/// not reference Core.Cli, and promoting that type into Core.Telemetry would drag the
/// PowerShell and TUI providers' shape along with it.
/// </summary>
public sealed class AgentTelemetryProvider : DefaultTelemetryProvider
{
    public AgentTelemetryProvider(
        IEnumerable<BaseProcessor<Activity>> filterProcessors,
        ITelemetrySender sender,
        IConfigurationService configuration)
    {
        // The name MUST stay in lockstep with CLITelemetryProvider. FileExporter writes
        // service.name from ActivitySource.Name, and TraceUploader rebuilds parent/child
        // edges only within a service-name group — so a different name here would silently
        // reparent every agent span to a random span id on upload, flattening the recovery
        // trace (Azure/CLIPS#602).
        var sourceName = ClientType.CLI.ToString();
        ActivitySource = new ActivitySource(sourceName);

        // Unlike the CLI, where every span goes through ITelemetryService and is gated
        // per call, the agent emits from AgentTracing.Source — a plain ActivitySource that
        // does not consult telemetry.enabled. Gating listener construction is what makes the
        // opt-out cover every agent span, tool.* included. Deciding once per process is fine
        // here: azclips-agent.exe is short-lived and single-shot.
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
