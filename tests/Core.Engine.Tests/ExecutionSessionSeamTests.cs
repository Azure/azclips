// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

    private sealed class ReportedHttpFailureProcessor : IProcessor
    {
        private IEventPublisher publisher = null!;

        public IProcessor Init(ProcessorContext context)
        {
            publisher = context.Publisher;
            return this;
        }

        public async Task ProcessAsync()
        {
            await publisher.Publish(Events.ResourceOperationFailed, "not found");
            throw new HttpRequestException("not found", inner: null, statusCode: System.Net.HttpStatusCode.NotFound);
        }
    }

    private sealed class SeamStubIndex : IExtensionIndex
    {
        private readonly CommandMetadata Meta;

        public SeamStubIndex(Type processorType)
        {
            Meta = new CommandMetadata(
                Name: "SeamStub",
                InvocationName: "x",
                Aliases: new List<string>(),
                FullName: processorType.FullName!,
                AssemblyName: processorType.Assembly.GetName().Name!,
                ProcessorType: processorType,
                Parameters: new Dictionary<string, ParameterMetadata>(),
                ParameterAliases: new Dictionary<string, string>());
        }

        public CommandMetadata GetCommandMetadata(string name, ClientType clientType) => Meta;
        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
            => new Dictionary<string, CommandMetadata> { ["x"] = Meta };
        public IEnumerable<string> GetCommandKeys(ClientType clientType) => new[] { "x" };
        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => new[] { Meta };
        public IReadOnlyList<string> GetVerbs() => new[] { "seam" };
    }

    private static ExecutionSession CreateSession(ClientType clientType, Type? processorType = null)
    {
        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams(noop, noop, noop, noop, noop, noop);
        var invocation = new CommandInvocation("seam", "x", new Dictionary<string, object>(), IsTransient: false);

        var index = new SeamStubIndex(processorType ?? typeof(SeamStubProcessor));
        var session = new ExecutionSession(clientType, invocation, streams)
        {
            GetExtensionVersion = () => string.Empty,
            CacheGetOrAdd = (_, _, _) => index,
            BindProcessor = (processor, _, _, _, _) => processor,
            ResolveProfileAsync = (s) => Task.FromResult<Profile?>(new Profile(){SubscriptionId = "sub-1"}),
            CreateProcessorContext =
                (s) => Task.FromResult(new ProcessorContext(null!, s.EventBus, null!, null!)),
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

    [Fact]
    public void Run_DoesNotRethrow_ReportedHttpFailure_ForPowerShell()
    {
        using var session = CreateSession(ClientType.PowerShell, typeof(ReportedHttpFailureProcessor));

        session.Run();
    }

    [Fact]
    public void Run_Rethrows_ReportedHttpFailure_ForCli()
    {
        using var session = CreateSession(ClientType.CLI, typeof(ReportedHttpFailureProcessor));

        Assert.Throws<HttpRequestException>(session.Run);
    }
}
