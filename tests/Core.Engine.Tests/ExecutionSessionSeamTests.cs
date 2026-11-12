// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Core.Runtime;

namespace Core.Engine.Tests;

[CollectionDefinition(nameof(ExecutionSessionSeamTests), DisableParallelization = true)]
public sealed class ExecutionSessionSeamTestsCollection { }

[Collection(nameof(ExecutionSessionSeamTests))]
public class ExecutionSessionSeamTests
{
    private sealed class SeamStubProcessor : IProcessor
    {
        public static bool WasExecuted;
        public IProcessor Init(ProcessorContext context) => this;
        public Task ProcessAsync()
        {
            WasExecuted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingStubProcessor : IProcessor
    {
        public IProcessor Init(ProcessorContext context) => this;
        public Task ProcessAsync() => throw new InvalidOperationException("Processor failed.");
    }

    private sealed class SeamStubIndex : IExtensionIndex
    {
        private static readonly CommandMetadata Meta = new(
            Name: "SeamStub",
            InvocationName: "x",
            Aliases: new List<string>(),
            FullName: typeof(SeamStubProcessor).FullName!,
            AssemblyName: typeof(SeamStubProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(SeamStubProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>());

        public CommandMetadata GetCommandMetadata(string name, ClientType clientType) => Meta;
        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
            => new Dictionary<string, CommandMetadata> { ["x"] = Meta };
        public IEnumerable<string> GetCommandKeys(ClientType clientType) => new[] { "x" };
        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => new[] { Meta };
        public IReadOnlyList<string> GetVerbs() => new[] { "seam" };
    }

    private static ExecutionSession CreateSession(ClientType clientType, OutputStreams? outputStreams = null)
    {
        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = outputStreams ?? new OutputStreams(noop, noop, noop, noop, noop, noop);
        var invocation = new CommandInvocation("seam", "x", new Dictionary<string, object>(), IsTransient: false);

        var index = new SeamStubIndex();
        var session = new ExecutionSession(clientType, invocation, streams)
        {
            GetExtensionVersion = () => string.Empty,
            CacheGetOrAdd = (_, _, _) => index,
            BindProcessor = (processor, _, _, _) => processor,
            ResolveProfileAsync = (s) => Task.FromResult<Profile?>(new Profile(){SubscriptionId = "sub-1"}),
            CreateProcessorContext =
                (s) => Task.FromResult(new ProcessorContext(null!, s.EventBus, null!, null!)),
            TagUploadRegion = _ => { },
            ResolveUploadRegionAsync = (_, _) => Task.FromResult(UploadRegion.EU),
        };
        return session;
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public async Task Execution_TracksEachCommand_AndRoutesSurveyToWarningStream(ClientType clientType)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"azclips-survey-session-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "azclips-survey.json");
        var warnings = new List<string>();
        var output = new List<object>();
        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams(
            WriteOutput: (value, _) => { output.Add(value); return Task.CompletedTask; },
            WriteVerbose: noop,
            WriteDebug: noop,
            WriteWarning: (value, _) => { warnings.Add((string)value); return Task.CompletedTask; },
            WriteInformation: noop,
            WriteError: noop);
        var tracker = new SurveyCommandTracker(new Core.Runtime.ScopedLoggingService(), () => true, path);
        try
        {
            for (int count = 1; count <= 7; count++)
            {
                using var session = CreateSession(clientType, streams);
                session.EventBus.Subscribe(Events.ProcessorAcquired,
                    (object _) => tracker.RecordCommandAsync(clientType, "1.0.0", session.EventBus.Token));
                if (clientType == ClientType.CLI)
                    await session.RunAsync();
                else
                    session.Run();

                Assert.Equal(count < 6 ? 0 : 1, warnings.Count);
                using var state = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
                Assert.Equal(count, state.RootElement.GetProperty("commandcount")
                    .GetProperty(clientType == ClientType.CLI ? "cli" : "powershell").GetInt32());
            }

            var expectedMessage = clientType == ClientType.CLI
                ? "[Survey] Tell us what you think of Azure CLI. This survey should take about 2 minutes. Run 'az survey' to open in browser."
                : "[Survey] Tell us what you think of Azure PowerShell. This survey should take about 2 minutes. Run 'Open-AzSurvey' to open in browser.";
            Assert.Equal(expectedMessage, Assert.Single(warnings));
            Assert.Empty(output);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Timing_SeparatesPreparationFromInvocation_WithoutProcessorEventsOrForcedLoads()
    {
        var clock = new ManualClock();
        var timeline = new CommandTimingRecorder(clock);
        using var session = CreateSession(ClientType.CLI);
        session.Performance = timeline;
        session.LoadAssembly = (_, _) => throw new InvalidOperationException("A cached index must not load a DLL.");
        session.ResolveProfileAsync = _ =>
        {
            clock.Advance(10);
            return Task.FromResult<Profile?>(null);
        };
        session.BindProcessor = (_, _, _, _) =>
        {
            clock.Advance(20);
            return new ActionProcessor(async () =>
            {
                var invocation = Assert.Single(timeline.Capture().Measurements, p => p.Name == "Command invocation");
                Assert.False(invocation.IsComplete);
                clock.Advance(30);
                await Task.Yield();
            });
        };

        await session.RunAsync();

        var rows = timeline.Capture().Measurements;
        var preparation = Assert.Single(rows, p => p.Name.StartsWith("Dispatch preparation"));
        var invocation = Assert.Single(rows, p => p.Name == "Command invocation");
        Assert.Equal(TimeSpan.FromMilliseconds(30), preparation.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(30), invocation.Duration);
        Assert.Equal(preparation.End, invocation.Start);
        Assert.DoesNotContain(rows, p => p.Name == "Selected-extension loading");
        Assert.All(rows, p => Assert.True(p.IsComplete));
    }

    [Fact]
    public async Task Timing_LoadFailureClosesLoadingAndPreparation_WithoutInvocation()
    {
        var clock = new ManualClock();
        var timeline = new CommandTimingRecorder(clock);
        using var session = CreateSession(ClientType.CLI);
        session.Performance = timeline;
        session.CacheGetOrAdd = (_, _, factory) => factory();
        session.LoadAssembly = (_, _) =>
        {
            clock.Advance(25);
            throw new InvalidOperationException("Load failed.");
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(session.RunAsync);

        Assert.Equal("Load failed.", error.Message);
        var rows = timeline.Capture().Measurements;
        var loading = Assert.Single(rows, p => p.Name == "Selected-extension loading");
        Assert.Equal(TimeSpan.FromMilliseconds(25), loading.Duration);
        Assert.Equal(1, loading.Depth);
        Assert.DoesNotContain(rows, p => p.Name == "Command invocation");
        Assert.All(rows, p => Assert.True(p.IsComplete));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Timing_FailureOrCancellationClosesInvocationAndPreservesException(bool canceled)
    {
        var clock = new ManualClock();
        var timeline = new CommandTimingRecorder(clock);
        using var session = CreateSession(ClientType.CLI);
        session.Performance = timeline;
        Exception expected = canceled ? new OperationCanceledException() : new InvalidOperationException("Processor failed.");
        session.BindProcessor = (_, _, _, _) => new ActionProcessor(() =>
        {
            clock.Advance(17);
            return Task.FromException(expected);
        });

        var error = await Record.ExceptionAsync(session.RunAsync);

        Assert.Same(expected, error);
        var invocation = Assert.Single(timeline.Capture().Measurements, p => p.Name == "Command invocation");
        Assert.True(invocation.IsComplete);
        Assert.Equal(TimeSpan.FromMilliseconds(17), invocation.Duration);
    }

    private sealed class ActionProcessor(Func<Task> process) : IProcessor
    {
        public IProcessor Init(ProcessorContext context) => this;
        public Task ProcessAsync() => process();
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;
        public override long GetTimestamp() => _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(int milliseconds) => _timestamp += TimeSpan.FromMilliseconds(milliseconds).Ticks;
    }

    // The engine publishes Events.ProcessorAcquired at the lifecycle seam: after the command
    // processor is fully resolved and bound, but BEFORE it executes (ProcessAsync). The startup
    // benchmark depends on this — a benchmark-mode subscriber (Engine.SubscribeBenchmark)
    // terminates the process when this event fires, so the spawned process is timed only up to
    // "ready". Here we verify the publication contract in-process; the Environment.Exit behaviour
    // of the benchmark subscriber itself is verified out-of-process (spawned az/pwsh).

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public async Task Execution_RecordsCommand_BeforeProcessorFailure(ClientType clientType)
    {
        using var session = CreateSession(clientType);
        session.BindProcessor = (_, _, _, _, _) => new FailingStubProcessor();
        int commandCount = 0;
        session.EventBus.Subscribe(Events.ProcessorAcquired, (object _) => { commandCount++; });

        if (clientType == ClientType.CLI)
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.RunAsync());
        else
            Assert.Throws<InvalidOperationException>(() => session.Run());

        Assert.Equal(1, commandCount);
    }

    [Fact]
    public async Task RunAsync_PublishesProcessorAcquired_BeforeExecutingProcessor()
    {
        SeamStubProcessor.WasExecuted = false;
        using var session = CreateSession(ClientType.CLI);

        int acquiredCount = 0;
        bool executedAtAcquire = true;
        session.EventBus.Subscribe(Events.ProcessorAcquired, (object _) =>
        {
            acquiredCount++;
            executedAtAcquire = SeamStubProcessor.WasExecuted; // must be false: event precedes execution
        });

        await session.RunAsync();

        Assert.Equal(1, acquiredCount);             // event published exactly once
        Assert.False(executedAtAcquire);            // it fired before ProcessAsync
        Assert.True(SeamStubProcessor.WasExecuted); // no halt subscriber here, so execution proceeds
    }

    [Fact]
    public void Run_PublishesProcessorAcquired_BeforeExecutingProcessor()
    {
        SeamStubProcessor.WasExecuted = false;
        using var session = CreateSession(ClientType.PowerShell);

        int acquiredCount = 0;
        bool executedAtAcquire = true;
        session.EventBus.Subscribe(Events.ProcessorAcquired, (object _) =>
        {
            acquiredCount++;
            executedAtAcquire = SeamStubProcessor.WasExecuted;
        });

        session.Run();

        Assert.Equal(1, acquiredCount);
        Assert.False(executedAtAcquire);
        Assert.True(SeamStubProcessor.WasExecuted);
    }

    // Regression coverage for the "root activity created/disposed by a different flow than the
    // one that resolves routing" bug: PowerShell cmdlets create the root Activity, then call
    // session.Run() (whose actual work happens on a forked worker task via ExecuteProcessorAsync)
    // and dispose the root only after Run() returns. A write to AsyncLocal-backed ambient state
    // made on that forked flow never propagates back to the cmdlet's own flow — so routing must
    // be recorded by tagging the root Activity object directly (an ordinary shared heap
    // reference), which stays visible to the caller regardless of which flow performs the
    // mutation. These tests hold a reference to the root Activity from OUTSIDE RunAsync (playing
    // the role of the calling cmdlet) and assert on it AFTER RunAsync returns, with a genuinely
    // yielding resolver (a real Task.Delay, mirroring real token-acquisition/ARM network calls).
    [Fact]
    public async Task RunAsync_TagsCallersRootActivity_WithResolvedDataBoundary()
    {
        using var source = new ActivitySource(nameof(RunAsync_TagsCallersRootActivity_WithResolvedDataBoundary));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => ReferenceEquals(s, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        // Played by the "caller" (e.g. a PowerShell cmdlet): creates the root Activity before
        // invoking the session, and inspects it only after the session call returns.
        using var rootActivity = source.StartActivity("TestRoot", ActivityKind.Server);
        Assert.NotNull(rootActivity);

        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams(noop, noop, noop, noop, noop, noop);
        var invocation = new CommandInvocation("seam", "x", new Dictionary<string, object>(), IsTransient: false);
        var index = new SeamStubIndex();

        using var session = new ExecutionSession(ClientType.CLI, invocation, streams)
        {
            GetExtensionVersion = () => string.Empty,
            CacheGetOrAdd = (_, _, _) => index,
            BindProcessor = (processor, _, _, _) => processor,
            ResolveProfileAsync = (_) => Task.FromResult<Profile?>(new Profile { TenantId = "tenant-1", SubscriptionId = "sub-1" }),
            CreateProcessorContext = (s) => Task.FromResult(new ProcessorContext(null!, s.EventBus, null!, null!)),
            TagUploadRegion = region =>
            {
                var root = Activity.Current;
                while (root?.Parent is not null) root = root.Parent;
                root?.SetTag(TelemetryTags.DataBoundary, region.ToDataBoundary());
            },
            ResolveUploadRegionAsync = async (_, cancellationToken) =>
            {
                // Genuine yield — mirrors the real MSAL/ARM network call that broke the
                // original AsyncLocal-based design.
                await Task.Delay(5, cancellationToken);
                return UploadRegion.EU;
            },
        };

        await session.RunAsync();

        Assert.Equal("EU", rootActivity.GetTagItem(TelemetryTags.DataBoundary));
        Assert.Null(rootActivity.GetTagItem(TelemetryTags.UploadRegion));
        Assert.Null(rootActivity.GetTagItem(TelemetryTags.DataBoundaryOutcome));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_TagsTenantUnavailable_WhenNoProfileIsResolved(bool synchronous)
    {
        using var source = new ActivitySource(nameof(RunAsync_TagsTenantUnavailable_WhenNoProfileIsResolved));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => ReferenceEquals(s, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var rootActivity = source.StartActivity("TestRoot", ActivityKind.Server);
        Assert.NotNull(rootActivity);

        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams(noop, noop, noop, noop, noop, noop);
        var invocation = new CommandInvocation("seam", "x", new Dictionary<string, object>(), IsTransient: false);
        var index = new SeamStubIndex();

        using var session = new ExecutionSession(ClientType.CLI, invocation, streams)
        {
            GetExtensionVersion = () => string.Empty,
            CacheGetOrAdd = (_, _, _) => index,
            BindProcessor = (processor, _, _, _) => processor,
            ResolveProfileAsync = (_) => Task.FromResult<Profile?>(null),
            CreateProcessorContext = (s) => Task.FromResult(new ProcessorContext(null!, s.EventBus, null!, null!)),
            TagUploadRegion = region =>
            {
                Assert.Equal(UploadRegion.ROW, region);
                rootActivity.SetTag(TelemetryTags.DataBoundary, region.ToDataBoundary());
            },
            ResolveUploadRegionAsync = (profile, _) =>
            {
                Assert.Null(profile);
                return Task.FromResult(UploadRegion.ROW);
            },
        };

        if (synchronous)
            session.Run();
        else
            await session.RunAsync();

        Assert.Equal("Global", rootActivity.GetTagItem(TelemetryTags.DataBoundary));
        Assert.Null(rootActivity.GetTagItem(TelemetryTags.UploadRegion));
        Assert.Null(rootActivity.GetTagItem(TelemetryTags.DataBoundaryOutcome));
    }
}
