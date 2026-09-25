// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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

    [Fact]
    public void ActualReplayPipeline_UsesTheRequestedResourceAndProfileWithoutNetwork()
    {
        using var noNetwork = TelemetryNetworkGuard.Disable();
        var captured = new List<(string? Role, ActivitySpanId Parent, object? Version)>();
        var sink = new TracerProviderExportSink([TelemetryTarget.Remote], (builder, target, boundary) =>
        {
            Assert.Equal(TelemetryTarget.Remote, target);
            Assert.Equal("EU", boundary);
            builder.SetSampler(new AlwaysOnSampler())
                .AddProcessor(new SimpleActivityExportProcessor(new ResourceExporter(captured)));
        });
        var entry = new TraceEntry
        {
            TraceId = "11111111111111111111111111111111",
            SpanId = "2222222222222222",
            OperationName = "Agent.Session",
            ParentSpanId = "0000000000000000",
            Kind = "Server",
            StartTime = "2024-01-01T00:00:00.0000000Z",
            Duration = 1,
            Tags = JsonSerializer.SerializeToElement(new[] { new { Key = "schemaVersion", Value = 1 } }),
            Events = JsonSerializer.SerializeToElement(Array.Empty<object>()),
        };
        using var unrelated = new Activity("ambient").Start();

        Assert.True(sink.TryExport("Agent", [entry], "EU", TraceReplayProfile.ManagedLayer));

        var actual = Assert.Single(captured);
        Assert.Equal("Agent", actual.Role);
        Assert.Equal(default, actual.Parent);
        Assert.Equal(1, actual.Version);
        Assert.Same(unrelated, Activity.Current);
    }

    [Fact]
    public void RealNetworkSink_IsRejectedBeforeAConnectionCanBeCreated()
    {
        using var noNetwork = TelemetryNetworkGuard.Disable();
        var sink = new TracerProviderExportSink([TelemetryTarget.Remote]);
        Assert.Throws<InvalidOperationException>(() => sink.TryExport("Agent", []));
    }

    private sealed class ResourceExporter(
        List<(string? Role, ActivitySpanId Parent, object? Version)> captured) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            var role = ParentProvider.GetResource().Attributes
                .Single(attribute => attribute.Key == "service.name").Value as string;
            foreach (var activity in batch)
                captured.Add((role, activity.ParentSpanId, activity.GetTagItem("schemaVersion")));
            return ExportResult.Success;
        }
    }
}
