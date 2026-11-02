// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Logging;
using Tests.Common.Utilities;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Xunit;
using Xunit.Abstractions;

namespace Core.Telemetry.Tests;

public class AriaActivityExporterTests
{
    private readonly ITestOutputHelper _output;

    public AriaActivityExporterTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void NetworkConstructors_AreRejectedBeforeBuildingTheTransport()
    {
        using var noNetwork = TelemetryNetworkGuard.Disable();
        Assert.Throws<InvalidOperationException>(() => new AriaActivityExporter());
        Assert.Throws<InvalidOperationException>(() =>
            new AriaActivityExporter(WellKnownEndpoints.AriaConnectionString));
        Assert.Throws<InvalidOperationException>(() => TelemetryExporters.AddTraceExporter(
            Sdk.CreateTracerProviderBuilder(), TelemetryTarget.Remote));
    }

    [Fact]
    public void Export_SendsActivityToAria()
    {
        using var noNetwork = TelemetryNetworkGuard.Disable();
        using var logs = new RecordingLoggerFactory();
        var exporter = new AriaActivityExporter(
            WellKnownEndpoints.AriaConnectionString, WellKnownEndpoints.AriaRowEndpoint,
            (connection, endpoint) =>
            {
                Assert.Equal(WellKnownEndpoints.AriaConnectionString, connection);
                Assert.Equal(WellKnownEndpoints.AriaRowEndpoint, endpoint);
                return logs;
            });
        using var source = new ActivitySource("AriaIntegrationTest");
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource("AriaIntegrationTest")
            .AddProcessor(new SimpleActivityExportProcessor(exporter));

        using var provider = builder.Build()!;

        using (var activity = source.StartActivity("TestCommand", ActivityKind.Server))
        {
            Assert.NotNull(activity);
            activity.SetTag(TelemetryTags.CommandName, "aria-integration-test");
            activity.SetTag("command.exit_code", 0);
            activity.SetTag(TelemetryTags.InstallationId, "test-install-id");
            activity.SetTag(TelemetryTags.ProductVersion, "0.1.0-test");
        }

        var entry = Assert.Single(logs.Entries);
        Assert.Equal("TestCommand", entry["operationName"]);
        Assert.False(entry.ContainsKey("OperationName"));
        Assert.Equal("aria-integration-test", entry[TelemetryTags.CommandName]);
        Assert.Equal("test-install-id", entry[TelemetryTags.InstallationId]);
    }

    [Fact]
    public void Export_SendsActivityToAriaEu()
    {
        using var noNetwork = TelemetryNetworkGuard.Disable();
        using var logs = new RecordingLoggerFactory();
        var exporter = new AriaActivityExporter(
            WellKnownEndpoints.AriaConnectionString,
            WellKnownEndpoints.AriaEuEndpoint,
            (connection, endpoint) =>
            {
                Assert.Equal(WellKnownEndpoints.AriaConnectionString, connection);
                Assert.Equal(WellKnownEndpoints.AriaEuEndpoint, endpoint);
                return logs;
            });
        using var source = new ActivitySource("AriaEuIntegrationTest");
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource("AriaEuIntegrationTest")
            .AddProcessor(new SimpleActivityExportProcessor(exporter));

        using var provider = builder.Build()!;

        using (var activity = source.StartActivity("TestCommandEu", ActivityKind.Server))
        {
            Assert.NotNull(activity);
            activity.SetTag(TelemetryTags.CommandName, "aria-integration-test-eu");
            activity.SetTag("command.exit_code", 0);
            activity.SetTag(TelemetryTags.InstallationId, "test-install-id-eu");
            activity.SetTag(TelemetryTags.ProductVersion, "0.1.0-test-eu");
            activity.SetTag(TelemetryTags.UploadRegion, "EU");
        }

        var entry = Assert.Single(logs.Entries);
        Assert.Equal("EU", entry["dataBoundary"]);
        Assert.False(entry.ContainsKey("uploadRegion"));
    }

    [Fact]
    public void Export_RoutesDynamically_BasedOnPerActivityUploadRegion()
    {
        using var noNetwork = TelemetryNetworkGuard.Disable();
        using var global = new RecordingLoggerFactory();
        using var eu = new RecordingLoggerFactory();
        var exporter = new AriaActivityExporter((_, endpoint) =>
            endpoint == WellKnownEndpoints.AriaEuEndpoint ? eu : global);
        using var source = new ActivitySource("AriaDynamicRoutingTest");
        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource("AriaDynamicRoutingTest")
            .AddProcessor(new SimpleActivityExportProcessor(exporter));

        using var provider = builder.Build()!;

        using (var activity = source.StartActivity("DynamicGlobalCommand", ActivityKind.Server))
        {
            Assert.NotNull(activity);
            activity.SetTag(TelemetryTags.CommandName, "dynamic-routing-global");
            activity.SetTag(TelemetryTags.UploadRegion, "ROW");
        }

        using (var activity = source.StartActivity("DynamicEuCommand", ActivityKind.Server))
        {
            Assert.NotNull(activity);
            activity.SetTag(TelemetryTags.CommandName, "dynamic-routing-eu");
            activity.SetTag(TelemetryTags.UploadRegion, "EU");
        }

        var globalEntry = Assert.Single(global.Entries);
        var euEntry = Assert.Single(eu.Entries);
        Assert.Equal("Global", globalEntry["dataBoundary"]);
        Assert.Equal("EU", euEntry["dataBoundary"]);
        Assert.False(globalEntry.ContainsKey("uploadRegion"));
        Assert.False(euEntry.ContainsKey("uploadRegion"));
    }

    [Theory]
    [InlineData("TenantUnavailable", true)]
    [InlineData("AuthenticationUnavailable", true)]
    [InlineData("BoundaryUnavailable", false)]
    [InlineData(null, false)]
    [InlineData("FutureOutcome", false)]
    public void Export_RoutesFallbackFromAncestorAndPersistsIt(string? outcome, bool row)
    {
        using var noNetwork = TelemetryNetworkGuard.Disable();
        using var global = new RecordingLoggerFactory();
        using var eu = new RecordingLoggerFactory();
        using var exporter = new AriaActivityExporter((_, endpoint) =>
            endpoint == WellKnownEndpoints.AriaEuEndpoint ? eu : global);
        var directory = Path.Combine(Path.GetTempPath(), "azclips-boundary-routing-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var fileExporter = new FileExporter(directory);
            using var root = new Activity("routing-root").Start();
            root.SetTag(TelemetryTags.DataBoundaryOutcome, outcome);
            using var child = new Activity("routing-child").Start();
            child.Stop();
            var batch = new Batch<Activity>([child], 1);
            Assert.Equal(ExportResult.Success, exporter.Export(in batch));
            Assert.Equal(ExportResult.Success, fileExporter.Export(in batch));
            var entry = Assert.Single(TraceFileFormat.Parse(File.ReadAllText(
                Assert.Single(Directory.GetFiles(directory, "traces-*.log")))));
            Assert.Null(entry.DataBoundaryOutcome);
            Assert.Null(entry.DataBoundary);
            Assert.Equal(row ? "ROW" : "EU", entry.UploadRegion);
            Assert.Single(row ? global.Entries : eu.Entries);
            Assert.Empty(row ? eu.Entries : global.Entries);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActualLayerPipeline_PreservesMetadataAndNativeGraphAcrossBothTargets(bool agent)
    {
        using var noNetwork = TelemetryNetworkGuard.Disable();
        var rootName = agent ? "Agent.Session" : "Test.Root";
        var childName = agent ? "Agent.Send" : "Test.Child";
        var leafName = agent ? "tool.get_command" : "Test.Other";
        var layer = agent ? TelemetryLayers.Agent : new TelemetryLayer(
            "TestLayer" + Guid.NewGuid().ToString("N"), "testlayer", "test-v1",
            [rootName, childName, leafName]);
        await using var environment = new TelemetryLayerTestEnvironment(layer: layer);
        var context = environment.Context();
        var scope = environment.Create(context);
        using var global = new RecordingLoggerFactory();
        using var eu = new RecordingLoggerFactory();
        var exported = new Dictionary<TelemetryTarget, List<ExportedSpan>>();
        var sink = new TracerProviderExportSink([TelemetryTarget.Local, TelemetryTarget.Remote],
            (builder, target, boundary) =>
            {
                Assert.Equal(UploadRegion.EU, boundary);
                var spans = new List<ExportedSpan>();
                exported.Add(target, spans);
                builder.AddProcessor(new SimpleActivityExportProcessor(new GraphExporter(spans)));
                if (target == TelemetryTarget.Remote)
                    builder.AddProcessor(new SimpleActivityExportProcessor(
                        new AriaActivityExporter((_, endpoint) =>
                            endpoint == WellKnownEndpoints.AriaEuEndpoint ? eu : global)));
            });
        var uploader = new TraceUploader(new TraceFileStore(environment.Root), sink, [layer]);
        using var ambient = new Activity("unrelated-caller").Start();
        using var rootScope = scope.Telemetry.StartIndependentRootActivity(rootName);
        var root = rootScope?.Activity;
        Assert.NotNull(root);
        var originalTrace = root.TraceId.ToString();
        using (var child = scope.Telemetry.StartActivity(childName))
        using (var leaf = scope.Telemetry.StartActivity(leafName))
        {
            Assert.NotNull(child);
            Assert.NotNull(leaf);
            leaf.SetTag("properties.testlayer.count", 42);
        }

        uploader.Run();
        Assert.Empty(exported);
        Assert.Empty(eu.Entries);
        Assert.Equal(TelemetryLayerCompletion.Published,
            await scope.CompleteAsync(root).WaitAsync(TelemetryLayerTestEnvironment.TestTimeout));
        rootScope!.Dispose();
        Assert.Same(ambient, Activity.Current);
        Assert.Equal(3, environment.Read(scope).Count);

        uploader.Run();

        Assert.Empty(Directory.GetFiles(environment.Root, "traces-*"));
        Assert.Empty(global.Entries);
        Assert.Equal(2, exported.Count);
        var expectedParents = new Dictionary<string, string?>
        {
            [rootName] = null,
            [childName] = rootName,
            [leafName] = childName,
        };
        foreach (var spans in exported.Values)
        {
            Assert.Equal(3, spans.Count);
            Assert.Equal(3, spans.Select(span => span.SpanId).Distinct().Count());
            Assert.All(spans, span =>
            {
                Assert.Equal(originalTrace, span.TraceId);
                Assert.Equal(layer.ServiceName, span.ServiceName);
                Assert.Equal(layer.ServiceName, span.Tags[TelemetryLayerTags.ServiceName]);
                Assert.Equal(layer.Schema, span.Tags[TelemetryLayerTags.Schema]);
                Assert.Equal(context.InvocationId, span.Tags[TelemetryLayerTags.InvocationId]);
                Assert.Equal("EU", span.Tags[TelemetryTags.UploadRegion]);
                var parent = span.ParentSpanId == "0000000000000000" ? null :
                    Assert.Single(spans, candidate => candidate.SpanId == span.ParentSpanId);
                Assert.Equal(expectedParents[span.Name], parent?.Name);
            });
            Assert.Equal(42, Assert.Single(spans, span => span.Name == leafName)
                .Tags["properties.testlayer.count"]);
        }

        Assert.Equal(3, eu.Entries.Count);
        foreach (var entry in eu.Entries)
        {
            Assert.Equal(layer.ServiceName, entry[TelemetryLayerTags.ServiceName]);
            Assert.Equal(layer.Schema, entry[TelemetryLayerTags.Schema]);
            Assert.Equal(context.InvocationId, entry[TelemetryLayerTags.InvocationId]);
            Assert.Equal(originalTrace, entry["traceId"]);
            Assert.Equal("EU", entry["dataBoundary"]);
            Assert.False(entry.ContainsKey("uploadRegion"));
            var span = Assert.Single(exported[TelemetryTarget.Remote],
                candidate => candidate.SpanId == Assert.IsType<string>(entry["spanId"]));
            if (span.ParentSpanId == "0000000000000000")
                Assert.False(entry.ContainsKey("parentSpanId"));
            else
                Assert.Equal(span.ParentSpanId, entry["parentSpanId"]);
            Assert.Equal(span.Name, entry["operationName"]);
        }
    }

    [Theory]
    [InlineData(UploadRegion.EU)]
    [InlineData(UploadRegion.ROW)]
    public async Task ActualAgentLayer_UsesRecordedUploadRegion(UploadRegion region)
    {
        using var noNetwork = TelemetryNetworkGuard.Disable();
        await using var environment = new TelemetryLayerTestEnvironment(layer: TelemetryLayers.Agent);
        var context = environment.Context() with { UploadRegion = region };
        var scope = environment.Create(context);
        using var root = scope.Telemetry.StartIndependentRootActivity("Agent.Session");
        Assert.NotNull(root);
        Assert.Equal(TelemetryLayerCompletion.Published,
            await scope.CompleteAsync(root.Activity).WaitAsync(TelemetryLayerTestEnvironment.TestTimeout));

        var expected = region.ToString();
        Assert.Equal(expected, Assert.Single(environment.Read(scope)).UploadRegion);
        using var global = new RecordingLoggerFactory();
        using var eu = new RecordingLoggerFactory();
        var sink = new TracerProviderExportSink([TelemetryTarget.Remote], (builder, _, actualBoundary) =>
        {
            Assert.Equal(region, actualBoundary);
            builder.AddProcessor(new SimpleActivityExportProcessor(
                new AriaActivityExporter((_, endpoint) =>
                    endpoint == WellKnownEndpoints.AriaEuEndpoint ? eu : global)));
        });

        new TraceUploader(new TraceFileStore(environment.Root), sink).Run();

        Assert.Empty(region == UploadRegion.EU ? global.Entries : eu.Entries);
        var entry = Assert.Single(region == UploadRegion.EU ? eu.Entries : global.Entries);
        Assert.Equal(region == UploadRegion.ROW ? "Global" : "EU", entry["dataBoundary"]);
        Assert.False(entry.ContainsKey("uploadRegion"));
        Assert.Empty(Directory.GetFiles(environment.Root, "traces-*"));
    }

    private sealed record ExportedSpan(
        string TraceId, string SpanId, string ParentSpanId, string Name, string ServiceName,
        IReadOnlyDictionary<string, object?> Tags);

    private sealed class GraphExporter(List<ExportedSpan> spans) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            var serviceName = Assert.IsType<string>(ParentProvider.GetResource().Attributes
                .Single(attribute => attribute.Key == "service.name").Value);
            foreach (var activity in batch)
                spans.Add(new ExportedSpan(activity.TraceId.ToString(), activity.SpanId.ToString(),
                    activity.ParentSpanId.ToString(), activity.OperationName, serviceName,
                    activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value)));
            return ExportResult.Success;
        }
    }

    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        internal List<Dictionary<string, object?>> Entries { get; } = [];
        public void AddProvider(ILoggerProvider provider) =>
            throw new InvalidOperationException("Network logging providers are not allowed in this test.");
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this);
        public void Dispose() { }

        private sealed class RecordingLogger(RecordingLoggerFactory owner) : ILogger
        {
            private Dictionary<string, object?> _scope = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                if (state is not IEnumerable<KeyValuePair<string, object?>> values)
                    throw new InvalidOperationException("Expected the exporter dictionary scope.");

                var previous = _scope;
                _scope = values.ToDictionary(pair => pair.Key, pair => pair.Value);
                return new ScopeLease(() => _scope = previous);
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var entry = new Dictionary<string, object?>(_scope);
                if (state is IEnumerable<KeyValuePair<string, object?>> values)
                {
                    foreach (var pair in values.Where(pair => pair.Key != "{OriginalFormat}"))
                        entry[pair.Key] = pair.Value;
                }
                owner.Entries.Add(entry);
            }
        }

        private sealed class ScopeLease(Action restore) : IDisposable
        {
            public void Dispose() => restore();
        }
    }

    [Fact]
    public void BufferedHttpSpan_PreservesInvocationMetadataThroughAriaReplay()
    {
        using var noNetwork = TelemetryNetworkGuard.Disable();
        var directory = Path.Combine(
            Path.GetTempPath(), "azclips-http-replay-" + Guid.NewGuid().ToString("N"));

        try
        {
            const string sourceName = nameof(BufferedHttpSpan_PreservesInvocationMetadataThroughAriaReplay);
            const string httpOperation = "System.Net.Http.HttpRequestOut";
            using var source = new ActivitySource(sourceName);
            using (var provider = Sdk.CreateTracerProviderBuilder()
                       .AddSource(sourceName)
                       .SetSampler(new AlwaysOnSampler())
                       .AddProcessor(new SimpleActivityExportProcessor(new FileExporter(directory)))
                       .Build())
            {
                using var root = source.StartActivity("CLI.Activity.Root", ActivityKind.Server);
                Assert.NotNull(root);
                root.SetTag(TelemetryTags.InstallationId, "install-1");
                root.SetTag(TelemetryTags.ProductName, "cli");
                root.SetTag(TelemetryTags.ProductVersion, "1.2.3");
                root.SetTag(TelemetryTags.OperatingSystemType, "windows");
                root.SetTag(TelemetryTags.UploadRegion, "ROW");
                root.SetTag("url.full", "https://private-host.example/private-path");

                using var http = source.StartActivity(httpOperation, ActivityKind.Client);
                Assert.NotNull(http);
                http.SetTag("http.request.method", "GET");
            }

            using var logs = new RecordingLoggerFactory();
            var sink = new TracerProviderExportSink([TelemetryTarget.Remote],
                (builder, _, _) => builder.AddProcessor(new SimpleActivityExportProcessor(
                    new AriaActivityExporter((_, _) => logs))));

            new TraceUploader(new TraceFileStore(directory), sink).Run();

            var entry = Assert.Single(logs.Entries,
                candidate => Equals(candidate.GetValueOrDefault("operationName"), httpOperation));
            Assert.Equal("install-1", entry[TelemetryTags.InstallationId]);
            Assert.Equal("cli", entry[TelemetryTags.ProductName]);
            Assert.Equal("1.2.3", entry[TelemetryTags.ProductVersion]);
            Assert.Equal("windows", entry[TelemetryTags.OperatingSystemType]);
            Assert.Equal("GET", entry["http.request.method"]);
            Assert.DoesNotContain("url.full", entry.Keys);
            Assert.DoesNotContain("private-host.example", entry.Values.OfType<string>());
        }
        finally
        {
            DirectoryUtils.TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public void BuildScope_MergesParentChainTags_IntoChildActivityScope()
    {
        // TenantId/UploadRegion/SubscriptionId are tagged once on the root Activity (see
        // ITelemetryService.AddTag) — child activities do NOT inherit tags in
        // System.Diagnostics.Activity, so BuildScope must walk the parent chain itself,
        // otherwise a child's Aria record silently loses the root's routing/identity metadata.
        using var source = new ActivitySource(nameof(BuildScope_MergesParentChainTags_IntoChildActivityScope));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => ReferenceEquals(s, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var root = source.StartActivity("root", ActivityKind.Server);
        Assert.NotNull(root);
        root.SetTag(TelemetryTags.TenantId, "tenant-1");
        root.SetTag(TelemetryTags.UploadRegion, "EU");

        using var child = source.StartActivity("child");
        Assert.NotNull(child);
        child.SetTag(TelemetryTags.CommandName, "child-op");

        var scope = AriaActivityExporter.BuildScope(child);

        Assert.Equal(child.TraceId.ToString(), scope["traceId"]);
        Assert.Equal(child.SpanId.ToString(), scope["spanId"]);
        Assert.Equal(child.ParentSpanId.ToString(), scope["parentSpanId"]);
        Assert.Equal(child.Duration.TotalMilliseconds, scope["durationMs"]);
        Assert.Equal(child.StartTimeUtc.ToString("o"), scope["startTime"]);
        Assert.Equal(child.Status.ToString(), scope["status"]);
        Assert.Equal(child.Kind.ToString(), scope["kind"]);
        Assert.DoesNotContain(
            new[] { "TraceId", "SpanId", "ParentSpanId", "DurationMs", "StartTime", "Status", "Kind" },
            scope.ContainsKey);
        Assert.Equal("tenant-1", scope[TelemetryTags.TenantId]);
        Assert.Equal("EU", scope["dataBoundary"]);
        Assert.False(scope.ContainsKey("uploadRegion"));
        // The child's own tag must still be present and win over any same-named ancestor tag.
        Assert.Equal("child-op", scope[TelemetryTags.CommandName]);
    }

    [Fact]
    public void BuildScope_DoesNotMergeDisallowedAncestorTags_IntoChildActivityScope()
    {
        // DefaultTelemetryPropertyFilterProcessor only strips disallowed tags off an Activity
        // when THAT Activity's own OnEnd fires — the root is still open (not yet disposed) when
        // a child exports, so its tags have not been filtered yet. BuildScope's parent-chain
        // merge must apply the same allowlist itself, or a disallowed tag temporarily sitting on
        // the root (e.g. URL/path data, which the allowlist explicitly excludes) would leak into
        // every child's exported record before the root's own filtering ever runs.
        using var source = new ActivitySource(nameof(BuildScope_DoesNotMergeDisallowedAncestorTags_IntoChildActivityScope));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => ReferenceEquals(s, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var root = source.StartActivity("root", ActivityKind.Server);
        Assert.NotNull(root);
        root.SetTag(TelemetryTags.TenantId, "tenant-1");
        root.SetTag("userAgent", "caller-supplied-sensitive-value");
        root.SetTag("http.url", "https://management.azure.com/subscriptions/secret-sub-id");
        root.SetTag("url.full", "https://management.azure.com/subscriptions/another-secret-sub-id");

        using var child = source.StartActivity("child");
        Assert.NotNull(child);

        var scope = AriaActivityExporter.BuildScope(child);

        Assert.Equal("tenant-1", scope[TelemetryTags.TenantId]);
        Assert.False(scope.ContainsKey("userAgent"));
        Assert.False(scope.ContainsKey("http.url"), "Disallowed ancestor tag must not be merged into the child's scope.");
        Assert.False(scope.ContainsKey("url.full"), "Disallowed ancestor tag must not be merged into the child's scope.");
    }
}
