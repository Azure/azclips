// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Agent.Tools.Resource.Cache;
using Core.Agent.Tools.Resource.Sku;
using Core.Telemetry;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors;
using Microsoft.Azure.Azclips.Extensions.Agent.Processors.Resource;
using Microsoft.Extensions.AI;

namespace Core.Agent.Tests;

[Collection("AgentTelemetry")]
public sealed class AgentToolCommandScopeTests
{
    [Theory]
    [InlineData("find", "tool.find_resources", "EU")]
    [InlineData("tree", "tool.get_resource_tree", "Global")]
    [InlineData("show", "tool.get_resource_details", "EU")]
    [InlineData("sku", "tool.find_available_locations", "Global")]
    public async Task RealProcessor_UsesCurrentCommandSnapshot_AndPreservesTheContractedCliGraph(
        string command, string toolName, string boundary)
    {
        using var cli = new AgentTraceCapture(sourceName: "CLI");
        await using var layer = new AgentLayerTestHarness();
        using var dependencies = new Dependencies(cli, layer);
        var commandName = $"agent tool resource {command}";
        var inheritedId = Guid.NewGuid().ToString("N");
        var inheritedInvocation = Guid.NewGuid().ToString("N");
        var launch = new Dictionary<string, string?>();
        TelemetryLayerEnvironment.SetAgentLaunch(launch,
            new TelemetryInvocationContext(inheritedInvocation, true, DataBoundary: boundary == "EU" ? "Global" : "EU"),
            inheritedId);
        var saved = launch.Keys.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        foreach (var (name, value) in launch) Environment.SetEnvironmentVariable(name, value);
        try
        {
            JsonElement recordedTool = default;
            string baselineOutput;
            using (var baselineRoot = cli.Telemetry.StartRootActivity())
            {
                TagCommand(baselineRoot!, commandName, boundary);
                using var processor = cli.Telemetry.StartActivity("ProcessorProcessAsyncStart");
                using var oldTool = cli.Telemetry.StartActivity(toolName);
                baselineOutput = await dependencies.InvokeBusinessAsync(command);
            }
            var baseline = cli.Stopped.ToArray();
            cli.Stopped.Clear();

            using (var root = cli.Telemetry.StartRootActivity())
            {
                TagCommand(root!, commandName, boundary);
                dependencies.CommandContext.BindCommand(root);
                var processor = dependencies.Processor(command);

                await processor.ProcessAsync();

                Assert.Same(root, Activity.Current);
                Assert.Equal(baselineOutput, Assert.Single(dependencies.Output));
                var scope = Assert.Single(dependencies.Factory.Scopes);
                Assert.Equal(root!.TraceId.ToString(), scope.Context.CommandTraceId);
                Assert.Equal(root.SpanId.ToString(), scope.Context.CommandSpanId);
                Assert.Equal("invocation-tenant", scope.Context.TenantId);
                Assert.Equal(boundary, scope.Context.EffectiveDataBoundary);
                Assert.NotEqual(inheritedInvocation, scope.Context.InvocationId);
                Assert.DoesNotContain(inheritedId, scope.FileSession.PublishedPath!);
                var entry = Assert.Single(AgentLayerTestHarness.ReadEntries(scope));
                recordedTool = entry;
                var tags = AgentLayerTestHarness.Tags(entry);
                Assert.Equal(toolName, entry.GetProperty("operationName").GetString());
                Assert.Equal("Internal", entry.GetProperty("kind").GetString());
                Assert.NotEqual(root.TraceId.ToString(), entry.GetProperty("traceId").GetString());
                Assert.Equal(root.TraceId.ToString(), tags[TelemetryTags.CommandTraceId].GetString());
                Assert.Equal(boundary, entry.GetProperty("dataBoundary").GetString());
                Assert.False(tags.ContainsKey(TelemetryTags.Parameter));
            }

            Assert.Equal(RetainedGraph(baseline), RetainedGraph(cli.Stopped.ToArray()));
            Assert.DoesNotContain(cli.Stopped.SelectMany(activity => activity.TagObjects),
                tag => tag.Key is TelemetryLayerTags.Schema or TelemetryLayerTags.ServiceName or TelemetryLayerTags.InvocationId);
            Assert.Equal(0, dependencies.Auth.AcquireCount);
            Assert.Equal(0, dependencies.HttpGuard.Requests);
            Assert.Equal("", recordedTool.GetProperty("parentSpanId").GetString());
        }
        finally
        {
            foreach (var (name, value) in saved) Environment.SetEnvironmentVariable(name, value);
        }
    }

    [Theory]
    [InlineData("find")]
    [InlineData("tree")]
    [InlineData("show")]
    [InlineData("sku")]
    public async Task RealProcessor_ExceptionCompletesItsScopeBeforeFallbackCanStart(string command)
    {
        using var cli = new AgentTraceCapture(sourceName: "CLI");
        await using var layer = new AgentLayerTestHarness();
        using var dependencies = new Dependencies(cli, layer) { FailOutput = true };
        using var root = cli.Telemetry.StartRootActivity();
        TagCommand(root!, $"agent tool resource {command}", "EU");
        dependencies.CommandContext.BindCommand(root);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => dependencies.Processor(command).ProcessAsync());

        Assert.Equal("offline output failure", error.Message);
        var completed = Assert.Single(dependencies.Factory.Scopes);
        Assert.NotNull(completed.FileSession.PublishedPath);
        await using var fallback = layer.Factory.CreateScope("Agent", dependencies.CommandContext.Capture());
        using var recoveryScope = fallback.Telemetry.StartIndependentRootActivity("CLI.Activity.AiRecovery");
        var recovery = recoveryScope?.Activity;
        Assert.NotNull(recovery);
        Assert.DoesNotContain("source_already_owned", fallback.Diagnostics);
        Assert.Equal(TelemetryLayerCompletion.Published, await fallback.CompleteAsync(recovery));
        Assert.NotEqual(completed.Context.InvocationId, fallback.Context.InvocationId);
    }

    [Theory]
    [InlineData("find")]
    [InlineData("tree")]
    [InlineData("show")]
    [InlineData("sku")]
    public async Task DisabledToolScope_PreservesOutputWithoutPublishingAFile(string command)
    {
        using var cli = new AgentTraceCapture(sourceName: "CLI");
        await using var layer = new AgentLayerTestHarness(enabled: false);
        using var dependencies = new Dependencies(cli, layer);
        using var root = cli.Telemetry.StartRootActivity();
        dependencies.CommandContext.BindCommand(root);

        await dependencies.Processor(command).ProcessAsync();

        Assert.Single(dependencies.Output);
        var scope = Assert.Single(dependencies.Factory.Scopes);
        Assert.False(scope.Context.Enabled);
        Assert.Null(scope.FileSession.PartialPath);
        Assert.Null(scope.FileSession.PublishedPath);
    }

    private static void TagCommand(Activity root, string name, string boundary)
    {
        root.SetTag(TelemetryTags.CommandName, name);
        root.SetTag(TelemetryTags.TenantId, "invocation-tenant");
        root.SetTag(TelemetryTags.DataBoundary, boundary);
        root.SetTag(TelemetryTags.Parameter, "private offline arguments");
    }

    private static string[] RetainedGraph(Activity[] activities)
    {
        var byId = activities.ToDictionary(activity => activity.SpanId);
        return activities.Where(activity => !activity.OperationName.StartsWith("tool.", StringComparison.Ordinal))
            .Select(activity =>
            {
                byId.TryGetValue(activity.ParentSpanId, out var parent);
                while (parent?.OperationName.StartsWith("tool.", StringComparison.Ordinal) == true)
                    byId.TryGetValue(parent.ParentSpanId, out parent);
                return $"{activity.OperationName}|{parent?.OperationName}|{string.Join(';', activity.TagObjects
                    .OrderBy(tag => tag.Key).Select(tag => $"{tag.Key}={tag.Value}"))}";
            }).OrderBy(row => row).ToArray();
    }

    private sealed class RecordingFactory(TelemetryLayerScopeFactory factory) : ITelemetryLayerScopeFactory
    {
        internal List<LayerTelemetryScope> Scopes { get; } = [];
        public ITelemetryLayerScope CreateScope(string layer, TelemetryInvocationContext context, string? fileId = null)
        {
            Assert.Equal("Agent", layer);
            Assert.Null(fileId);
            var scope = factory.CreateScope(layer, context, fileId);
            Scopes.Add(scope);
            return scope;
        }
    }

    private sealed class Dependencies : IDisposable
    {
        private readonly Dictionary<Type, object> _services;
        internal AgentOfflineAuth Auth { get; } = new();
        internal AgentOfflineProfiles Profiles { get; }
        internal AgentOfflineHttp HttpGuard { get; } = new();
        internal HttpClient Http { get; }
        internal CommandTelemetryContext CommandContext { get; }
        internal RecordingFactory Factory { get; }
        internal Publisher Publisher { get; }
        internal List<string> Output { get; } = [];
        internal bool FailOutput { get; set; }

        internal Dependencies(AgentTraceCapture cli, AgentLayerTestHarness layer)
        {
            Profiles = new AgentOfflineProfiles { Telemetry = cli.Telemetry };
            Http = new HttpClient(HttpGuard);
            CommandContext = new CommandTelemetryContext(layer.Configuration);
            Factory = new RecordingFactory(layer.Factory);
            Publisher = new Publisher(cli.Telemetry);
            _services = new()
            {
                [typeof(IAuthService)] = Auth, [typeof(IProfileService)] = Profiles,
                [typeof(HttpClient)] = Http, [typeof(ITelemetryLayerScopeFactory)] = Factory,
                [typeof(ICommandTelemetryContext)] = CommandContext,
            };
        }

        internal T Get<T>() where T : class => (T)_services[typeof(T)];
        internal Task Write(object value, CancellationToken _)
        {
            if (FailOutput) throw new InvalidOperationException("offline output failure");
            Output.Add(value.ToString()!);
            return Task.CompletedTask;
        }

        internal AgentProcessorBase Processor(string command) => command switch
        {
            "find" => new Find(this) { Name = "offline", Kind = "invalid" },
            "tree" => new Tree(this) { Name = "offline", Kind = "invalid" },
            "show" => new Show(this) { Name = "offline", Kind = "invalid" },
            _ => new Sku(this) { VmSize = "offline" },
        };

        internal async Task<string> InvokeBusinessAsync(string command)
        {
            var tools = new CachedResourceTools(new ArmDetailsClient(Auth), Profiles);
            var arguments = command switch
            {
                "find" => new AIFunctionArguments { ["namePattern"] = "offline", ["kind"] = "invalid", ["top"] = 10 },
                "tree" => new AIFunctionArguments { ["nameOrId"] = "offline", ["kind"] = "invalid", ["maxDepth"] = 2 },
                "show" => new AIFunctionArguments { ["nameOrId"] = "offline", ["kind"] = "invalid", ["live"] = false },
                _ => new AIFunctionArguments { ["vmSize"] = "offline" },
            };
            var function = command switch
            {
                "find" => tools.FindResources,
                "tree" => tools.GetResourceTree,
                "show" => tools.GetResourceDetails,
                _ => new AvailableLocationsTool(Auth, Profiles, Http).FindAvailableLocations,
            };
            return (await function.InvokeAsync(arguments))!.ToString()!;
        }

        public void Dispose()
        {
            Publisher.Dispose();
            Http.Dispose();
        }
    }

    private sealed class Find : ResourceFindProcessor
    {
        private readonly Dependencies _services;
        internal Find(Dependencies services) { _services = services; Publisher = services.Publisher; WriteOutput = services.Write; }
        protected override T GetService<T>() => _services.Get<T>();
    }
    private sealed class Tree : ResourceTreeProcessor
    {
        private readonly Dependencies _services;
        internal Tree(Dependencies services) { _services = services; Publisher = services.Publisher; WriteOutput = services.Write; }
        protected override T GetService<T>() => _services.Get<T>();
    }
    private sealed class Show : ResourceShowProcessor
    {
        private readonly Dependencies _services;
        internal Show(Dependencies services) { _services = services; Publisher = services.Publisher; WriteOutput = services.Write; }
        protected override T GetService<T>() => _services.Get<T>();
    }
    private sealed class Sku : ResourceSkuProcessor
    {
        private readonly Dependencies _services;
        internal Sku(Dependencies services) { _services = services; Publisher = services.Publisher; WriteOutput = services.Write; }
        protected override T GetService<T>() => _services.Get<T>();
    }

    private sealed class Publisher(ITelemetryService telemetry) : IEventPublisher, IDisposable
    {
        private Activity? _processor;
        public CancellationToken Token => CancellationToken.None;
        public Action Cancel => () => { };
        public Task Publish(string id)
        {
            if (id == Events.ProcessorProcessAsyncStart) _processor = telemetry.StartActivity(id);
            if (id == Events.ProcessorProcessAsyncEnd) _processor?.Dispose();
            return Task.CompletedTask;
        }
        public Task Publish(string id, CancellationToken token, Func<object> createMessage) => Publish(id);
        public Task Publish(string id, CancellationToken token) => Publish(id);
        public Task Publish(string id, CancellationToken token, string messageText) => Publish(id);
        public Task Publish(string id, CancellationToken token, string messageText, HttpRequestMessage request) => Publish(id);
        public Task Publish(string id, CancellationToken token, string messageText, HttpResponseMessage response) => Publish(id);
        public Task Publish(string id, CancellationToken token, string messageText, double magnitude) => Publish(id);
        public Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpRequestMessage request) => Publish(id);
        public Task Publish(string id, CancellationToken token, string messageText, double magnitude, HttpResponseMessage response) => Publish(id);
        public Task Publish(string id, CancellationToken token, HttpRequestMessage request) => Publish(id);
        public Task Publish(string id, CancellationToken token, HttpRequestMessage request, HttpResponseMessage response) => Publish(id);
        public Task Publish(string id, CancellationToken token, HttpResponseMessage response) => Publish(id);
        public Task Publish(string id, CancellationToken token, EventData message) => Publish(id);
        public Task Publish(string id, Func<object> createMessage) => Publish(id);
        public Task Publish(string id, string messageText) => Publish(id);
        public Task Publish(string id, string messageText, HttpRequestMessage request) => Publish(id);
        public Task Publish(string id, string messageText, HttpResponseMessage response) => Publish(id);
        public Task Publish(string id, string messageText, double magnitude) => Publish(id);
        public Task Publish(string id, string messageText, double magnitude, HttpRequestMessage request) => Publish(id);
        public Task Publish(string id, string messageText, double magnitude, HttpResponseMessage response) => Publish(id);
        public Task Publish(string id, HttpRequestMessage request) => Publish(id);
        public Task Publish(string id, HttpRequestMessage request, HttpResponseMessage response) => Publish(id);
        public Task Publish(string id, HttpResponseMessage response) => Publish(id);
        public Task Publish(string id, EventData message) => Publish(id);
        public Task Publish(string id, Uri uri) => Publish(id);
        public void Dispose() => _processor?.Dispose();
    }
}
