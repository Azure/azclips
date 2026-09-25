// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Agent.Processors
{
    public class AgentProcessorBase : IProcessor
    {
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventPublisher Publisher = null!;
        protected OutputStreamDelegate WriteOutput = null!;

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor Init(ProcessorContext context)
        {
            HttpPipeline = context.HttpPipeline;
            Publisher = context.Publisher;
            WriteOutput = context.OutputHandler.WriteOutput;
            return this;
        }

        public virtual Task ProcessAsync() => Task.CompletedTask;

        protected virtual T GetService<T>() where T : class =>
            AzclipsServiceProvider.GetRequiredService<T>();

        protected async Task RunToolCommandAsync(Func<Func<string, IDisposable?>, Task> run)
        {
            var context = GetService<ICommandTelemetryContext>().Capture();
            var scope = GetService<ITelemetryLayerScopeFactory>().CreateScope("Agent", context);
            try
            {
                using var producer = scope.TryEnterProducer();
                await run(name => scope.Telemetry.StartIndependentRootActivity(
                    name, ActivityKind.Internal, activate: false)).ConfigureAwait(false);
            }
            finally
            {
                // No inherited Agent launch/file metadata is read here. The command's bound
                // routing snapshot is the only source, and fallback cannot overtake this finally.
                try
                {
                    await scope.CompleteAsync().ConfigureAwait(false);
                }
                catch
                {
                    Trace.TraceWarning("Agent tool telemetry completion failed.");
                }
                try
                {
                    await scope.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    Trace.TraceWarning("Agent tool telemetry disposal failed.");
                }
            }
        }

        protected virtual HttpRequestMessage CreateRequestMessage()
            => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");
    }
}
