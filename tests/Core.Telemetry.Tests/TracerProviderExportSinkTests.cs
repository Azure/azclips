// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Telemetry.Tests;

public class TracerProviderExportSinkTests
{
    // Construction only: TryExport would build a live provider and block on ForceFlush.
    // TraceUploaderTests exercises TraceUploader through a fake sink; it does not cover
    // this class's fan-out loop.
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
    public void Constructor_Throws_WhenTargetsIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TracerProviderExportSink(null!));
    }

    [Fact]
    public void Constructor_Throws_WhenTargetsIsEmpty()
    {
        Assert.Throws<ArgumentException>(
            () => new TracerProviderExportSink([]));
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

    // --- ExportToTargets fan-out loop ---

    [Fact]
    public void ExportToTargets_LocalRemote_LocalFails_ReturnsTrueFromRemote()
    {
        // Remote is authoritative; a stopped Jaeger must not pin trace files.
        var result = TracerProviderExportSink.ExportToTargets(
            [TelemetryTarget.Local, TelemetryTarget.Remote],
            target => target == TelemetryTarget.Remote);

        Assert.True(result);
    }

    [Fact]
    public void ExportToTargets_LocalRemote_RemoteFails_ReturnsFalse()
    {
        var result = TracerProviderExportSink.ExportToTargets(
            [TelemetryTarget.Local, TelemetryTarget.Remote],
            target => target == TelemetryTarget.Local);

        Assert.False(result);
    }

    [Fact]
    public void ExportToTargets_LocalOnly_LocalFails_ReturnsFalse()
    {
        // Developer chose local-only explicitly; a failed delivery is retried.
        var result = TracerProviderExportSink.ExportToTargets(
            [TelemetryTarget.Local],
            _ => false);

        Assert.False(result);
    }

    [Fact]
    public void ExportToTargets_BothTargets_BothAreAttempted()
    {
        var invoked = new List<TelemetryTarget>();

        TracerProviderExportSink.ExportToTargets(
            [TelemetryTarget.Local, TelemetryTarget.Remote],
            target =>
            {
                invoked.Add(target);
                return true;
            });

        Assert.Contains(TelemetryTarget.Local, invoked);
        Assert.Contains(TelemetryTarget.Remote, invoked);
    }
}
