// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Core.Telemetry;

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
            RoutingContext = new NoopTelemetryRoutingContext(),
            ResolveDataBoundaryAsync = (_, _) => Task.FromResult<string?>(null),
        };
        return session;
    }

    /// <summary>No-op stand-in so RunAsync/Run's direct RoutingContext.Set(...) calls don't NRE.</summary>
    private sealed class NoopTelemetryRoutingContext : ITelemetryRoutingContext
    {
        public void Set(string? tenantId, string? dataBoundary) { }
        public (string? TenantId, string? DataBoundary) Current => (null, null);
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

    // Regression coverage for the AsyncLocal propagation bug: a write to an AsyncLocal-backed
    // ITelemetryRoutingContext made inside an awaited helper/event subscriber is discarded once
    // that helper returns — it never becomes visible to code that runs later in a DIFFERENT
    // method. RunAsync/Run must call RoutingContext.Set(...) directly in their own bodies. These
    // tests use a real AsyncLocal-backed context and a genuinely-yielding resolver (a real
    // Task.Delay, mirroring real token-acquisition/ARM network calls) — the exact shape that
    // broke the original (event-subscriber-based) design.
    [Fact]
    public async Task RunAsync_MakesResolvedDataBoundary_VisibleToProcessorProcessAsync()
    {
        RoutingCaptureProcessor.Observed = null;
        var routingContext = new AsyncLocalTelemetryRoutingContext();

        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams(noop, noop, noop, noop, noop, noop);
        var invocation = new CommandInvocation("seam", "x", new Dictionary<string, object>(), IsTransient: false);
        var index = new RoutingCaptureStubIndex();

        using var session = new ExecutionSession(ClientType.CLI, invocation, streams)
        {
            GetExtensionVersion = () => string.Empty,
            CacheGetOrAdd = (_, _, _) => index,
            BindProcessor = (processor, _, _, _, _) => processor,
            ResolveProfileAsync = (_) => Task.FromResult<Profile?>(new Profile { TenantId = "tenant-1", SubscriptionId = "sub-1" }),
            CreateProcessorContext = (s) => Task.FromResult(new ProcessorContext(null!, s.EventBus, null!, null!)),
            RoutingContext = routingContext,
            ResolveDataBoundaryAsync = async (_, cancellationToken) =>
            {
                // Genuine yield — mirrors the real MSAL/ARM network calls that broke the
                // original design (a Set() made inside a nested awaited callback never
                // flowed back to the caller once that callback returned).
                await Task.Delay(5, cancellationToken);
                return "EU";
            },
        };

        RoutingCaptureProcessor.Reader = () => routingContext.Current;

        await session.RunAsync();

        Assert.Equal(("tenant-1", "EU"), RoutingCaptureProcessor.Observed);
    }

    [Fact]
    public async Task RunAsync_ClearsRoutingContext_WhenNoProfileIsResolved()
    {
        RoutingCaptureProcessor.Observed = null;
        var routingContext = new AsyncLocalTelemetryRoutingContext();
        routingContext.Set("stale-tenant", "EU"); // simulate a value left over from elsewhere

        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams(noop, noop, noop, noop, noop, noop);
        var invocation = new CommandInvocation("seam", "x", new Dictionary<string, object>(), IsTransient: false);
        var index = new RoutingCaptureStubIndex();

        using var session = new ExecutionSession(ClientType.CLI, invocation, streams)
        {
            GetExtensionVersion = () => string.Empty,
            CacheGetOrAdd = (_, _, _) => index,
            BindProcessor = (processor, _, _, _, _) => processor,
            ResolveProfileAsync = (_) => Task.FromResult<Profile?>(null),
            CreateProcessorContext = (s) => Task.FromResult(new ProcessorContext(null!, s.EventBus, null!, null!)),
            RoutingContext = routingContext,
            ResolveDataBoundaryAsync = (_, _) => throw new InvalidOperationException("Should not be called when no profile is resolved."),
        };

        RoutingCaptureProcessor.Reader = () => routingContext.Current;

        await session.RunAsync();

        Assert.Equal((null, null), RoutingCaptureProcessor.Observed);
    }

    private sealed class AsyncLocalTelemetryRoutingContext : ITelemetryRoutingContext
    {
        private readonly AsyncLocal<(string? TenantId, string? DataBoundary)> _state = new();

        public void Set(string? tenantId, string? dataBoundary) => _state.Value = (tenantId, dataBoundary);

        public (string? TenantId, string? DataBoundary) Current => _state.Value;
    }

    private sealed class RoutingCaptureProcessor : IProcessor
    {
        public static Func<(string? TenantId, string? DataBoundary)>? Reader;
        public static (string? TenantId, string? DataBoundary)? Observed;

        public IProcessor Init(ProcessorContext context) => this;

        public Task ProcessAsync()
        {
            Observed = Reader?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class RoutingCaptureStubIndex : IExtensionIndex
    {
        private static readonly CommandMetadata Meta = new(
            Name: "RoutingCaptureStub",
            InvocationName: "x",
            Aliases: new List<string>(),
            FullName: typeof(RoutingCaptureProcessor).FullName!,
            AssemblyName: typeof(RoutingCaptureProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(RoutingCaptureProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>());

        public CommandMetadata GetCommandMetadata(string name, ClientType clientType) => Meta;
        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
            => new Dictionary<string, CommandMetadata> { ["x"] = Meta };
        public IEnumerable<string> GetCommandKeys(ClientType clientType) => new[] { "x" };
        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => new[] { Meta };
        public IReadOnlyList<string> GetVerbs() => new[] { "seam" };
    }
}
