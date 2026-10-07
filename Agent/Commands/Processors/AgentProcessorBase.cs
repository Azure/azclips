// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            var telemetry = GetService<ITelemetryService>();
            await run(name => telemetry.StartActivity(name)).ConfigureAwait(false);
        }

        protected virtual HttpRequestMessage CreateRequestMessage()
            => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");
    }
}
