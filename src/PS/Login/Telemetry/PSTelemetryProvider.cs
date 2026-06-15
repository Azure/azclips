// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Azure.Cirrus.Cmdlets.Login.Telemetry;

public class PSTelemetryProvider : DefaultTelemetryProvider
{
    public PSTelemetryProvider(IConfigurationService configurationService, IEnumerable<BaseProcessor<Activity>> filterProcessors)
    {
        var isTelemetryEnabled = configurationService.GetConfiguration<TelemetryOptions>().Enabled;
        if (!isTelemetryEnabled) return;

        var sender = new DirectTelemetrySender();
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

        sender.ConfigureExporter(builder, string.Empty);
        TracerProvider = builder.Build();
    }
}
