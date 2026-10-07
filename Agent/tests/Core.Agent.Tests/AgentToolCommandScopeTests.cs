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
        using var dependencies = new Dependencies(cli);
        var commandName = $"agent tool resource {command}";
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
            var processor = dependencies.Processor(command);

            await processor.ProcessAsync();

            Assert.Same(root, Activity.Current);
            Assert.Equal(baselineOutput, Assert.Single(dependencies.Output));
            var stopped = cli.Stopped.ToArray();
            var tool = Assert.Single(stopped, activity => activity.OperationName == toolName);
            Assert.Equal(root!.TraceId, tool.TraceId);
            Assert.DoesNotContain(tool.TagObjects, tag => tag.Key == TelemetryTags.Parameter);
        }

        Assert.Equal(RetainedGraph(baseline), RetainedGraph(cli.Stopped.ToArray()));
        Assert.DoesNotContain(cli.Stopped.SelectMany(activity => activity.TagObjects),
            tag => tag.Key is TelemetryLayerTags.Schema or TelemetryLayerTags.ServiceName or TelemetryLayerTags.InvocationId);
        Assert.Equal(0, dependencies.Auth.AcquireCount);
        Assert.Equal(0, dependencies.HttpGuard.Requests);
    }

    [Theory]
    [InlineData("find")]
    [InlineData("tree")]
    [InlineData("show")]
    [InlineData("sku")]
    public async Task RealProcessor_ExceptionCompletesItsScopeBeforeFallbackCanStart(string command)
    {
        using var cli = new AgentTraceCapture(sourceName: "CLI");
        using var dependencies = new Dependencies(cli) { FailOutput = true };
        using var root = cli.Telemetry.StartRootActivity();
        TagCommand(root!, $"agent tool resource {command}", "EU");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => dependencies.Processor(command).ProcessAsync());

        Assert.Equal("offline output failure", error.Message);
        Assert.Contains(cli.Stopped, activity => activity.OperationName == ToolName(command));
    }

    [Theory]
    [InlineData("find")]
    [InlineData("tree")]
    [InlineData("show")]
    [InlineData("sku")]
    public async Task DisabledToolScope_PreservesOutputWithoutPublishingAFile(string command)
    {
        using var cli = new AgentTraceCapture(sourceName: "CLI");
        using var dependencies = new Dependencies(cli);
        using var root = cli.Telemetry.StartRootActivity();

        await dependencies.Processor(command).ProcessAsync();

        Assert.Single(dependencies.Output);
        Assert.DoesNotContain(cli.Stopped.SelectMany(activity => activity.TagObjects),
            tag => tag.Key is TelemetryLayerTags.Schema or TelemetryLayerTags.ServiceName or TelemetryLayerTags.InvocationId);
    }

    private static void TagCommand(Activity root, string name, string boundary)
    {
        root.SetTag(TelemetryTags.Command, name);
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

    private static string ToolName(string command) => command switch
    {
        "find" => "tool.find_resources",
        "tree" => "tool.get_resource_tree",
        "show" => "tool.get_resource_details",
        _ => "tool.find_available_locations",
    };

    private sealed class Dependencies : IDisposable
    {
        private readonly Dictionary<Type, object> _services;
        internal AgentOfflineAuth Auth { get; } = new();
        internal AgentOfflineProfiles Profiles { get; }
        internal AgentOfflineHttp HttpGuard { get; } = new();
        internal HttpClient Http { get; }
        internal Publisher Publisher { get; }
        internal List<string> Output { get; } = [];
        internal bool FailOutput { get; set; }

        internal Dependencies(AgentTraceCapture cli)
        {
            Profiles = new AgentOfflineProfiles { Telemetry = cli.Telemetry };
            Http = new HttpClient(HttpGuard);
            Publisher = new Publisher(cli.Telemetry);
            _services = new()
            {
                [typeof(IAuthService)] = Auth, [typeof(IProfileService)] = Profiles,
                [typeof(HttpClient)] = Http, [typeof(ITelemetryService)] = cli.Telemetry,
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
