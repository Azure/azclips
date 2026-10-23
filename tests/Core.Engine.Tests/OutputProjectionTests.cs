// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Core.Runtime.Handler;

namespace Core.Engine.Tests;

public class OutputProjectionTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task Runtime_ProjectsOnceAfterBinding_WithoutChangingProcessorOutput(
        bool asynchronous, bool project, bool suppress)
    {
        var envelope = new Response("payload");
        var written = new List<object>();
        int callerThread = Environment.CurrentManagedThreadId;
        int? outputThread = null;
        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams((value, _) =>
        {
            written.Add(value);
            outputThread = Environment.CurrentManagedThreadId;
            return Task.CompletedTask;
        }, noop, noop, noop, noop, noop);
        OutputProcessor? boundProcessor = null;
        var index = new OutputIndex(project ? typeof(ProjectingProcessor) : typeof(OutputProcessor));
        using var session = new ExecutionSession(
            asynchronous ? ClientType.CLI : ClientType.PowerShell,
            new CommandInvocation("test", "test", new Dictionary<string, object>()),
            streams)
        {
            GetExtensionVersion = () => "",
            CacheGetOrAdd = (_, _, _) => index,
            ResolveProfileAsync = _ => Task.FromResult<Profile?>(null),
            CreateProcessorContext = value => Task.FromResult(new ProcessorContext(
                null!, value.EventBus, null!, new OutputHandler { WriteOutput = value.ProcessorOutputWriter })),
            BindProcessor = (processor, _, _, _, _) =>
            {
                boundProcessor = Assert.IsAssignableFrom<OutputProcessor>(processor);
                boundProcessor.Output = envelope;
                if (processor is ProjectingProcessor projection)
                    projection.Suppress = suppress;
                return processor;
            }
        };

        if (asynchronous)
            await session.RunAsync();
        else
            session.Run();

        Assert.NotNull(boundProcessor);
        Assert.Same(envelope, boundProcessor.Emitted);
        if (project && suppress)
        {
            Assert.Empty(written);
        }
        else if (project)
        {
            Assert.Equal("payload", Assert.Single(written));
        }
        else
        {
            Assert.Same(envelope, Assert.Single(written));
        }
        if (boundProcessor is ProjectingProcessor projecting)
            Assert.Equal(1, projecting.ProjectionCount);
        if (!asynchronous && written.Count > 0)
            Assert.Equal(callerThread, outputThread);
    }

    private sealed record Response(string Content);

    public class OutputProcessor : IProcessor
    {
        private OutputStreamDelegate writeOutput = null!;
        public object Output { get; set; } = null!;
        public object? Emitted { get; private set; }

        public IProcessor Init(ProcessorContext context)
        {
            writeOutput = context.OutputHandler.WriteOutput;
            return this;
        }

        public async Task ProcessAsync()
        {
            await Task.Yield();
            Emitted = Output;
            await writeOutput(Output);
        }
    }

    public sealed class ProjectingProcessor : OutputProcessor, IOutputProjection
    {
        public bool Suppress { get; set; }
        public int ProjectionCount { get; private set; }

        public object? GetOutputValue(object value)
        {
            ProjectionCount++;
            return Suppress ? null : Assert.IsType<Response>(value).Content;
        }
    }

    private sealed class OutputIndex(Type processorType) : IExtensionIndex
    {
        private readonly CommandMetadata metadata = new(
            "test", "test", [], processorType.FullName!, processorType.Assembly.GetName().Name!,
            processorType, new Dictionary<string, ParameterMetadata>(), new Dictionary<string, string>());

        public CommandMetadata GetCommandMetadata(string name, ClientType clientType) => metadata;
        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
            => new Dictionary<string, CommandMetadata> { ["test"] = metadata };
        public IEnumerable<string> GetCommandKeys(ClientType clientType) => ["test"];
        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => [metadata];
        public IReadOnlyList<string> GetVerbs() => ["test"];
    }
}
