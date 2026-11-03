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

        Assert.Equal(WellKnownEndpoints.AriaConnectionString, exporter.ConnectionString);
        Assert.Equal(WellKnownEndpoints.AriaEuEndpoint, exporter.Endpoint);
    }

    [Fact]
    public void ResolveAriaExporter_ReturnsRowEndpoint_ForConfirmedGlobalBoundary()
    {
        var exporter = TelemetryExporters.ResolveAriaExporter("Global");

        Assert.Equal(WellKnownEndpoints.AriaConnectionString, exporter.ConnectionString);
        Assert.Equal(WellKnownEndpoints.AriaRowEndpoint, exporter.Endpoint);
    }

    // Fail-closed: a missing/unresolved/unrecognized boundary must NEVER be treated as a
    // confirmed non-EU tenant (which "rounding to Global" would do) -- only ARM's genuine
    // "Global" answer may route to ROW. See DataBoundaryRouting.IsConfirmedGlobal.
    // (null is deliberately not covered here: ResolveAriaExporter(null) reads the active
    // profile's cache via DataBoundaryResolver, which depends on ambient AZCLIPS_HOME state
    // rather than being a pure function of the input string.)
    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("APAC")] // unrecognized/future value
    public void ResolveAriaExporter_ReturnsEuEndpoint_ForUnconfirmedBoundary(string? dataBoundary)
    {
        var exporter = TelemetryExporters.ResolveAriaExporter(dataBoundary);

        Assert.Equal(WellKnownEndpoints.AriaConnectionString, exporter.ConnectionString);
        Assert.Equal(WellKnownEndpoints.AriaEuEndpoint, exporter.Endpoint);
    }
}
