// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OpenTelemetry;
using OpenTelemetry.Trace;
using Core.Abstractions;

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
        var exporter = TelemetryExporters.ResolveAriaExporter(UploadRegion.EU);

        Assert.Equal(WellKnownEndpoints.AriaEuConnectionString, exporter.ConnectionString);
        Assert.Equal(WellKnownEndpoints.AriaEuEndpoint, exporter.Endpoint);
    }

    [Fact]
    public void ResolveAriaExporter_ReturnsRowEndpoint_ForConfirmedGlobalBoundary()
    {
        var exporter = TelemetryExporters.ResolveAriaExporter(UploadRegion.ROW);

        Assert.Equal(WellKnownEndpoints.AriaConnectionString, exporter.ConnectionString);
        Assert.Equal(WellKnownEndpoints.AriaRowEndpoint, exporter.Endpoint);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(999)]
    public void ResolveAriaExporter_RejectsInvalidRegion(int region)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TelemetryExporters.ResolveAriaExporter((UploadRegion)region));
    }
}
