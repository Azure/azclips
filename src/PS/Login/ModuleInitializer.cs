// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;
using Core.Telemetry;
using Microsoft.Azure.Azclips.Cmdlets.Login.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Azclips.Cmdlets.Login;

public class ModuleInitializer : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    public void OnImport()
    {
        var timeline = new PerformanceTimeline(ImportTelemetry.PerformanceSpanName);
        var importKind = ImportTelemetry.NextImportKind();

        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
            // A default the host may replace: prepends run before the framework defaults and
            // before every override. Nothing in RegisterTelemetryServices registers
            // ITelemetrySender, so this is what PowerShell resolves.
            AzclipsServiceProvider.PrependRegistration(services =>
                services.AddSingleton<ITelemetrySender, DirectTelemetrySender>());

            // Overrides, because RegisterTelemetryServices registers DefaultTelemetryProvider
            // among the defaults and a prepend would lose to it.
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
                services.AddSingleton<ITelemetryProvider, PSTelemetryProvider>();
                services.AddSingleton<IUserAgentHandler, PSUserAgentHandler>();
            });
            AzclipsServiceProvider.Initialize(ClientType.PowerShell);
        }

        ImportTelemetry.EmitInBackground(timeline, importKind, DateTime.UtcNow);
    }

    public void OnRemove(PSModuleInfo module)
    {
        AzclipsServiceProvider.Dispose();
    }
}
