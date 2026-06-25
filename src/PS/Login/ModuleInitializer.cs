// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;
using Microsoft.Azure.Azclips.Cmdlets.Login.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Azclips.Cmdlets.Login;

public class ModuleInitializer : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    public void OnImport()
    {
        CirrusServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
            services.AddSingleton<ITelemetryProvider, PSTelemetryProvider>();

        });
        CirrusServiceProvider.Initialize(ClientType.PowerShell);
    }

    public void OnRemove(PSModuleInfo module)
    {
        CirrusServiceProvider.Dispose();
    }
}
