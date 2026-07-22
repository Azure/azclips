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
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
            services.AddSingleton<ITelemetryProvider, PSTelemetryProvider>();
            services.AddSingleton<IUserAgentHandler, PSUserAgentHandler>();
        });
        AzclipsServiceProvider.Initialize(ClientType.PowerShell);
    }

    public void OnRemove(PSModuleInfo module)
    {
        AzclipsServiceProvider.Dispose();
    }
}
