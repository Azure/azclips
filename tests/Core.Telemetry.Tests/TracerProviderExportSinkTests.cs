// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TracerProviderExportSinkTests
{
    // Construction only: TryExport would build a live provider and block on ForceFlush.
    // TraceUploaderTests covers the behaviour through a fake.
    [Theory]
    [InlineData(TelemetryTarget.Local)]
    [InlineData(TelemetryTarget.Remote)]
    internal void Constructor_CreatesSink_ForASingleTarget(TelemetryTarget target)
    {
        ITraceExportSink sink = new TracerProviderExportSink([target]);

        Assert.NotNull(sink);
    }

    [Fact]
    public void Constructor_CreatesSink_ForBothTargets()
    {
        ITraceExportSink sink = new TracerProviderExportSink(
            [TelemetryTarget.Local, TelemetryTarget.Remote]);

        Assert.NotNull(sink);
    }

    [Fact]
    public void SelectAuthoritative_PrefersRemote_WhenBothConfigured()
    {
        // Remote owns retention, so a stopped local collector never pins trace files.
        Assert.Equal(
            TelemetryTarget.Remote,
            TracerProviderExportSink.SelectAuthoritative(
                [TelemetryTarget.Local, TelemetryTarget.Remote]));
    }

    [Fact]
    public void SelectAuthoritative_PrefersRemote_RegardlessOfOrder()
    {
        Assert.Equal(
            TelemetryTarget.Remote,
            TracerProviderExportSink.SelectAuthoritative(
                [TelemetryTarget.Remote, TelemetryTarget.Local]));
    }

    [Fact]
    public void SelectAuthoritative_ReturnsRemote_WhenRemoteOnly()
    {
        Assert.Equal(
            TelemetryTarget.Remote,
            TracerProviderExportSink.SelectAuthoritative([TelemetryTarget.Remote]));
    }

    [Fact]
    public void SelectAuthoritative_ReturnsLocal_WhenLocalOnly()
    {
        // Local-only is an explicit choice, so a failed export is retried.
        Assert.Equal(
            TelemetryTarget.Local,
            TracerProviderExportSink.SelectAuthoritative([TelemetryTarget.Local]));
    }
}
