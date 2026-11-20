// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;

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

    [Fact]
    public void RegisterTelemetryServices_SkipsFilterProcessors_OnlyWhenRulesAreBypassed()
    {
        var filtered = new ServiceCollection().RegisterTelemetryServices(bypassRules: false);
        var bypassed = new ServiceCollection().RegisterTelemetryServices(bypassRules: true);

        Assert.Equal(
            new[] { typeof(DefaultActivityNameFilterProcessor), typeof(DefaultTelemetryPropertyFilterProcessor) },
            filtered.Where(IsActivityProcessor).Select(descriptor => descriptor.ImplementationType!).ToArray());
        Assert.DoesNotContain(bypassed, IsActivityProcessor);

        static bool IsActivityProcessor(ServiceDescriptor descriptor) =>
            descriptor.ServiceType == typeof(BaseProcessor<Activity>);
    }
}
