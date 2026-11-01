// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
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

    [Theory]
    [InlineData("EU", false)]
    [InlineData("Global", false)]
    [InlineData("EU", true)]
    [InlineData("Global", true)]
    [InlineData("Unknown", false)]
    [InlineData("Unknown", true)]
    [InlineData("APAC", false)]
    public void Uploader_PreservesFullTreeAcrossServicesAndTargets(string boundary, bool retryAfterAgentFailure)
    {
        using var noNetwork = TelemetryNetworkGuard.Disable();
        using var directory = new TemporaryDirectory();
        const string traceId = "0123456789abcdef0123456789abcdef";
        const string commandSpanId = "1111111111111111";
        const string recoverySpanId = "2222222222222222";
        const string sessionSpanId = "3333333333333333";
        var invocationId = Guid.NewGuid().ToString("N");
        var reservations = new TraceUploadReservationStore(directory.Path);
        reservations.Begin(traceId, invocationId);
        var failAgent = retryAfterAgentFailure;
        var byTarget = new Dictionary<TelemetryTarget,
            List<(string Service, string Name, ActivityTraceId Trace, ActivitySpanId Span, ActivitySpanId Parent)>>();
        var sink = new TracerProviderExportSink([TelemetryTarget.Local, TelemetryTarget.Remote],
            (builder, target, actualBoundary) =>
            {
                Assert.Equal(DataBoundaryRouting.Normalize(boundary), actualBoundary);
                if (!byTarget.TryGetValue(target, out var exported))
                    byTarget[target] = exported = [];
                builder.SetSampler(new AlwaysOnSampler())
                    .AddProcessor(new SimpleActivityExportProcessor(new ChainExporter(exported)))
                    .AddProcessor(new AgentFlushFailure(() => failAgent));
            });

        WriteRecord(directory.Path, TraceFileStore.FileNameFor(traceId),
            CreateRecord(traceId, commandSpanId, "0000000000000000", "CLI.Activity.Root", "CLI", boundary),
            CreateRecord(traceId, recoverySpanId, commandSpanId, "CLI.Activity.AiRecovery", "CLI", boundary));
        WriteRecord(directory.Path, $"traces-agent-{invocationId}.log",
            CreateRecord(traceId, "6666666666666666", "5555555555555555", "tool.hook_pre",
                "Agent", boundary, invocationId),
            CreateRecord(traceId, "5555555555555555", sessionSpanId, "Agent.Send",
                "Agent", boundary, invocationId),
            CreateRecord(traceId, "4444444444444444", sessionSpanId, "Agent.Create",
                "Agent", boundary, invocationId),
            CreateRecord(traceId, sessionSpanId, recoverySpanId, "Agent.Session", "Agent", boundary, invocationId));
        reservations.MarkReady(invocationId);

        new TraceUploader(new TraceFileStore(directory.Path), sink).Run();
        if (retryAfterAgentFailure)
        {
            var retained = Directory.GetFiles(directory.Path, "traces-*.uploading");
            Assert.Equal(2, retained.Length);
            Assert.Single(Directory.GetFiles(directory.Path, "traces-hold-*.json"));
            foreach (var file in retained)
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-10));
            byTarget.Clear();
            failAgent = false;
            new TraceUploader(new TraceFileStore(directory.Path), sink).Run();
        }

        Assert.Equal(2, byTarget.Count);
        foreach (var exported in byTarget.Values)
        {
            var command = Assert.Single(exported, activity => activity.Name == "CLI.Activity.Root");
            var recovery = Assert.Single(exported, activity => activity.Name == "CLI.Activity.AiRecovery");
            var session = Assert.Single(exported, activity => activity.Name == "Agent.Session");
            var create = Assert.Single(exported, activity => activity.Name == "Agent.Create");
            var send = Assert.Single(exported, activity => activity.Name == "Agent.Send");
            var callback = Assert.Single(exported, activity => activity.Name == "tool.hook_pre");
            Assert.Equal("CLI", command.Service);
            Assert.Equal("CLI", recovery.Service);
            Assert.All(exported.Where(activity => !activity.Name.StartsWith("CLI.", StringComparison.Ordinal)),
                activity => Assert.Equal("Agent", activity.Service));
            Assert.All(exported,
                activity => Assert.Equal(ActivityTraceId.CreateFromString(traceId.AsSpan()), activity.Trace));
            Assert.NotEqual(ActivitySpanId.CreateFromString(commandSpanId.AsSpan()), command.Span);
            Assert.Equal(command.Span, recovery.Parent);
            Assert.Equal(recovery.Span, session.Parent);
            Assert.Equal(session.Span, create.Parent);
            Assert.Equal(session.Span, send.Parent);
            Assert.Equal(send.Span, callback.Parent);
        }
        Assert.NotEqual(byTarget[TelemetryTarget.Local][0].Span, byTarget[TelemetryTarget.Remote][0].Span);
        Assert.Empty(Directory.GetFiles(directory.Path, "traces-*"));
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

    private sealed class ChainExporter(
        List<(string Service, string Name, ActivityTraceId Trace, ActivitySpanId Span, ActivitySpanId Parent)> captured)
        : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            var service = ParentProvider.GetResource().Attributes
                .Single(attribute => attribute.Key == "service.name").Value as string ?? string.Empty;
            foreach (var activity in batch)
                captured.Add((service, activity.OperationName, activity.TraceId, activity.SpanId, activity.ParentSpanId));
            return ExportResult.Success;
        }
    }

    private sealed class AgentFlushFailure(Func<bool> fail) : BaseProcessor<Activity>
    {
        protected override bool OnForceFlush(int timeoutMilliseconds) =>
            !fail() || ParentProvider.GetResource().Attributes
                .Single(attribute => attribute.Key == "service.name").Value is not "Agent";
    }

    private static object CreateRecord(
        string traceId, string spanId, string parentSpanId, string name,
        string service, string boundary, string? invocationId = null)
    {
        var tags = invocationId is null
            ? Array.Empty<object>()
            : new object[]
            {
                new { Key = TelemetryLayerTags.Schema, Value = "agent-v1" },
                new { Key = TelemetryLayerTags.ServiceName, Value = "Agent" },
                new { Key = TelemetryLayerTags.InvocationId, Value = invocationId },
                new { Key = TelemetryTags.DataBoundary, Value = boundary },
            };
        return new
        {
            traceId,
            spanId,
            parentSpanId,
            operationName = name,
            startTime = "2024-01-01T00:00:00.0000000Z",
            durationMs = 1.0,
            status = "Unset",
            kind = "Internal",
            dataBoundary = boundary,
            tags,
            events = Array.Empty<object>(),
            resource = new Dictionary<string, string> { ["service.name"] = service },
        };
    }

    private static void WriteRecord(string directory, string name, params object[] records) =>
        File.WriteAllText(Path.Combine(directory, name),
            TraceFileFormat.FormatBatch(records, DateTime.UtcNow));

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "telemetry-replay-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
