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

    [Fact]
    public void ResolveAriaExporter_ReturnsEuEndpoint_ForEuBoundary()
    {
        var exporter = TelemetryExporters.ResolveAriaExporter("EU");

        Assert.Equal(WellKnownEndpoints.AriaEuConnectionString, exporter.ConnectionString);
        Assert.Equal(WellKnownEndpoints.AriaEuEndpoint, exporter.Endpoint);
    }

    [Theory]
    [InlineData("Global")]
    [InlineData("unknown")]
    public void ResolveAriaExporter_ReturnsGlobalEndpoint_ForNonEuBoundary(string? dataBoundary)
    {
        var exporter = TelemetryExporters.ResolveAriaExporter(dataBoundary);

        Assert.Equal(WellKnownEndpoints.AriaConnectionString, exporter.ConnectionString);
        Assert.Null(exporter.Endpoint);
    }
}
