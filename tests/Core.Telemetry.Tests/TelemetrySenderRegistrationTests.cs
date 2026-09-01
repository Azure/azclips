// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;

namespace Core.Telemetry.Tests;

public class TelemetrySenderRegistrationTests
{
    [Fact]
    public void RegisterTelemetryServices_ShouldRegisterNoTelemetrySender_SoHostsPickTheirOwn()
    {
        // ModuleInitializer registers DirectTelemetrySender in the PREPEND phase, which runs
        // before these defaults. A default sender here would clobber it and silently disable
        // PowerShell's telemetry export.
        var services = new ServiceCollection();
        services.RegisterTelemetryServices();

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ITelemetrySender));
    }
}
