// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;
using Core.Telemetry;
using Microsoft.Azure.Azclips.Cmdlets.Accounts.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

public class ModuleInitializer : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    private const string FirstPartyApplicationId = "1950a258-227b-4e31-a9cf-717495945fc2";

    public void OnImport()
    {
        var timeline = new PerformanceTimeline(ImportTelemetry.PerformanceSpanName);
        var importKind = ImportTelemetry.NextImportKind();

        using (timeline.Measure(ImportTelemetry.ContainerSpanName))
        {
            AzclipsServiceProvider.PrependRegistration(services =>
                services.AddSingleton<ITelemetrySender, DirectTelemetrySender>());

            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
                services.AddSingleton<ITelemetryProvider, PSTelemetryProvider>();
                services.AddSingleton<IUserAgentHandler, PSUserAgentHandler>();
            });
            AzclipsServiceProvider.Initialize(
                new ClientMetadata(
                    ClientType.PowerShell,
                    FirstPartyApplicationId,
                    PowerShellReleaseVersionResolver.GetReleaseVersion()));
        }

        ImportTelemetry.EmitInBackground(timeline, importKind, DateTime.UtcNow);
    }

    public void OnRemove(PSModuleInfo module)
    {
        AzclipsServiceProvider.Dispose();
    }
}
