// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Cmdlets.Tests;

/// <summary>
/// Locks the registration-phase contract that lets a host — PSBenchmarkBase — supply its own
/// telemetry sender to the PowerShell module.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class ModuleInitializerSenderRegistrationTests
{
    [Fact]
    public void OnImport_ShouldResolveDirectSender_WhenHostRegistersNone()
    {
        AzclipsServiceProvider.Dispose();
        try
        {
            new ModuleInitializer().OnImport();

            Assert.IsType<DirectTelemetrySender>(
                AzclipsServiceProvider.GetRequiredService<ITelemetrySender>());
            Assert.Equal(PowerShellReleaseVersionResolver.GetReleaseVersion(),
                AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ReleaseVersion);
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }

    [Fact]
    public void OnImport_ShouldResolveHostSender_WhenHostOverridesBeforeImport()
    {
        AzclipsServiceProvider.Dispose();
        try
        {
            // Mirrors PSBenchmarkBase: the host enqueues its override, then Import-Module runs
            // ModuleInitializer.OnImport. The override wins.
            AzclipsServiceProvider.OverrideRegistration(services =>
                services.AddSingleton<ITelemetrySender>(new StubTelemetrySender()));

            new ModuleInitializer().OnImport();

            Assert.IsType<StubTelemetrySender>(
                AzclipsServiceProvider.GetRequiredService<ITelemetrySender>());
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }

    private sealed class StubTelemetrySender : ITelemetrySender
    {
        public void ConfigureExporter(TracerProviderBuilder builder) { }
    }
}
