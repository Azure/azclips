// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;

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

    private static ExecutionSession CreateSession(ClientType clientType)
    {
        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams(noop, noop, noop, noop, noop, noop);
        var invocation = new CommandInvocation("seam", "x", new Dictionary<string, object>(), IsTransient: false);

        var index = new SeamStubIndex();
        var session = new ExecutionSession(clientType, invocation, streams)
        {
            GetExtensionVersion = () => string.Empty,
            CacheGetOrAdd = (_, _, _) => index,
            BindProcessor = (processor, _, _, _, _) => processor,
            ResolveProfileAsync = (s) => Task.FromResult<Profile?>(new Profile(){SubscriptionId = "sub-1"}),
            CreateProcessorContext =
                (s) => Task.FromResult(new ProcessorContext(null!, s.EventBus, null!, null!)),
            TagDataBoundary = _ => { },
            ResolveDataBoundaryAsync = (_, _) => Task.FromResult<string?>(null),
        };
        return session;
    }

    // The engine publishes Events.ProcessorAcquired at the lifecycle seam: after the command
    // processor is fully resolved and bound, but BEFORE it executes (ProcessAsync). The startup
    // benchmark depends on this — a benchmark-mode subscriber (Engine.SubscribeBenchmark)
    // terminates the process when this event fires, so the spawned process is timed only up to
    // "ready". Here we verify the publication contract in-process; the Environment.Exit behaviour
    // of the benchmark subscriber itself is verified out-of-process (spawned az/pwsh).

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
            BindProcessor = (processor, _, _, _, _) => processor,
            ResolveProfileAsync = (_) => Task.FromResult<Profile?>(new Profile { TenantId = "tenant-1", SubscriptionId = "sub-1" }),
            CreateProcessorContext = (s) => Task.FromResult(new ProcessorContext(null!, s.EventBus, null!, null!)),
            TagDataBoundary = boundary =>
            {
                var root = Activity.Current;
                while (root?.Parent is not null) root = root.Parent;
                root?.SetTag(TelemetryTags.DataBoundary, boundary);
            },
            ResolveDataBoundaryAsync = async (_, cancellationToken) =>
            {
                // Genuine yield — mirrors the real MSAL/ARM network call that broke the
                // original AsyncLocal-based design.
                await Task.Delay(5, cancellationToken);
                return "EU";
            },
        };

        await session.RunAsync();

        Assert.Equal("EU", rootActivity.GetTagItem(TelemetryTags.DataBoundary));
    }

    [Fact]
    public async Task RunAsync_DoesNotTagRootActivity_WhenNoProfileIsResolved()
    {
        using var source = new ActivitySource(nameof(RunAsync_DoesNotTagRootActivity_WhenNoProfileIsResolved));
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
            BindProcessor = (processor, _, _, _, _) => processor,
            ResolveProfileAsync = (_) => Task.FromResult<Profile?>(null),
            CreateProcessorContext = (s) => Task.FromResult(new ProcessorContext(null!, s.EventBus, null!, null!)),
            TagDataBoundary = _ => Assert.Fail("Should not be called when no profile is resolved."),
            ResolveDataBoundaryAsync = (_, _) => throw new InvalidOperationException("Should not be called when no profile is resolved."),
        };

        await session.RunAsync();

        Assert.Null(rootActivity.GetTagItem(TelemetryTags.DataBoundary));
    }

    private sealed class LocalStubIndex : IExtensionIndex
    {
        private static readonly CommandMetadata Meta = new(
            Name: "LocalStub",
            InvocationName: "x",
            Aliases: new List<string>(),
            FullName: typeof(SeamStubProcessor).FullName!,
            AssemblyName: typeof(SeamStubProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(SeamStubProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>())
        { IsLocal = true };

        public CommandMetadata GetCommandMetadata(string name, ClientType clientType) => Meta;
        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
            => new Dictionary<string, CommandMetadata> { ["x"] = Meta };
        public IEnumerable<string> GetCommandKeys(ClientType clientType) => new[] { "x" };
        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => new[] { Meta };
        public IReadOnlyList<string> GetVerbs() => new[] { "seam" };
    }

    // Offline builders (e.g. New-AzVirtualNetworkSubnetConfig) must not touch the profile or the
    // data-boundary preflight, which can acquire tokens and call ARM on an uncached tenant.
    [Fact]
    public void Run_LocalCommand_SkipsProfileAndDataBoundaryPreflight()
    {
        SeamStubProcessor.WasExecuted = false;
        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams(noop, noop, noop, noop, noop, noop);
        var invocation = new CommandInvocation("seam", "x", new Dictionary<string, object>(), IsTransient: false);
        var index = new LocalStubIndex();

        using var session = new ExecutionSession(ClientType.PowerShell, invocation, streams)
        {
            GetExtensionVersion = () => string.Empty,
            CacheGetOrAdd = (_, _, _) => index,
            BindProcessor = (processor, _, _, _, _) => processor,
            ResolveProfileAsync = _ => throw new InvalidOperationException("Local commands must not resolve a profile."),
            CreateProcessorContext = (s) => Task.FromResult(new ProcessorContext(null!, s.EventBus, null!, null!)),
            TagDataBoundary = _ => Assert.Fail("Local commands must not tag a data boundary."),
            ResolveDataBoundaryAsync = (_, _) => throw new InvalidOperationException("Local commands must not resolve a data boundary."),
        };

        session.Run();

        Assert.True(SeamStubProcessor.WasExecuted);
    }
}
