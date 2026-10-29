// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Core.Tui.Telemetry;

public class TUITelemetryProvider : DefaultTelemetryProvider
{
    // The Enabled check no longer lives here — TelemetryService gates every
    // Start*Activity call at runtime so telemetry.enabled changes take effect on
    // the next command without requiring a TUI restart.
    public TUITelemetryProvider(
        IEnumerable<BaseProcessor<Activity>> filterProcessors,
        ITelemetrySender sender,
        IConfigurationService configuration)
    {
        ActivitySource = new ActivitySource(ClientType.TUI.ToString());
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(ClientType.TUI.ToString())
            .ConfigureResource(r => r.AddService(ProcessUtils.GetRunningProcess()));

        builder.AddHttpClientInstrumentation(options =>
        {
            options.FilterHttpRequestMessage = _ => HttpActivityEnricher.ShouldCollect(configuration);
            options.RecordException = true;
            options.EnrichWithHttpRequestMessage = HttpActivityEnricher.Enrich;
        });

        foreach (var processor in filterProcessors)
        {
            builder.AddProcessor(processor);
        }

        sender.ConfigureExporter(builder);
        TracerProvider = builder.Build()!;
    }
}
