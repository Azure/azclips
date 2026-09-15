// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Core.Telemetry.Tests;

public class TelemetryExportersTests
{
    [Theory]
    [InlineData(TelemetryTarget.Local)]
    [InlineData(TelemetryTarget.Remote)]
    internal void AddTraceExporter_BuildsProvider_WithoutThrowing(TelemetryTarget target)
    {
        var builder = Sdk.CreateTracerProviderBuilder().AddSource("test-source");

        TelemetryExporters.AddTraceExporter(builder, target);

        using var provider = builder.Build();
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddTraceExporter_UnknownTarget_Throws()
    {
        var builder = Sdk.CreateTracerProviderBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TelemetryExporters.AddTraceExporter(builder, (TelemetryTarget)999));
    }

    [Fact]
    public void AddTraceExporters_BuildsProvider_ForASingleTarget()
    {
        var builder = Sdk.CreateTracerProviderBuilder().AddSource("test-source");

        TelemetryExporters.AddTraceExporters(builder, [TelemetryTarget.Local]);

        using var provider = builder.Build();
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddTraceExporters_BuildsProvider_ForBothTargets()
    {
        var builder = Sdk.CreateTracerProviderBuilder().AddSource("test-source");

        TelemetryExporters.AddTraceExporters(builder, [TelemetryTarget.Local, TelemetryTarget.Remote]);

        using var provider = builder.Build();
        Assert.NotNull(provider);
    }
}
