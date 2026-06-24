// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;

namespace Core.Engine.Tests;

public class ExecutionSessionSeamTests
{
    private const string SeamEnvVar = "CIRRUS_BENCH_RESOLVE_ONLY";

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

        var session = new ExecutionSession(clientType, invocation, streams)
        {
            GetExtensionVersion = () => string.Empty,
            LoadAssembly = (_, _) => throw new InvalidOperationException("LoadAssembly must not be called"),
            CacheGetOrAdd = (_, _, _) => new SeamStubIndex(),
            CreateProcessorContext = _ => Task.FromResult<ProcessorContext>(null!),
            BindProcessor = (p, _, _, _) => p,
        };
        return session;
    }

    [Fact]
    public async Task RunAsync_ShouldNotExecuteProcessor_WhenResolveOnlySet()
    {
        SeamStubProcessor.WasExecuted = false;
        Environment.SetEnvironmentVariable(SeamEnvVar, "1");
        try
        {
            using var session = CreateSession(ClientType.CLI);
            await session.RunAsync();
            Assert.False(SeamStubProcessor.WasExecuted);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SeamEnvVar, null);
        }
    }

    [Fact]
    public async Task RunAsync_ShouldExecuteProcessor_WhenResolveOnlyNotSet()
    {
        SeamStubProcessor.WasExecuted = false;
        Environment.SetEnvironmentVariable(SeamEnvVar, null);
        using var session = CreateSession(ClientType.CLI);
        await session.RunAsync();
        Assert.True(SeamStubProcessor.WasExecuted);
    }

    [Fact]
    public void Run_ShouldNotExecuteProcessor_WhenResolveOnlySet()
    {
        SeamStubProcessor.WasExecuted = false;
        Environment.SetEnvironmentVariable(SeamEnvVar, "1");
        try
        {
            using var session = CreateSession(ClientType.PowerShell);
            session.Run();
            Assert.False(SeamStubProcessor.WasExecuted);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SeamEnvVar, null);
        }
    }
}
