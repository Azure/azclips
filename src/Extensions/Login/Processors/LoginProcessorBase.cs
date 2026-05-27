using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Processors
{
    public class LoginProcessorBase : IProcessor
    {
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventListener EventListener = null!;
        protected readonly JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;

        protected virtual void BeforeInit() { }
        protected virtual void AfterInit() { }

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor Init(ProcessorContext context)
        {
            BeforeInit();

            HttpPipeline = context.HttpPipeline;
            EventListener = context.EventListener;
            WriteOutput = context.OutputHandler.WriteOutput;

            AfterInit();
            return this;
        }

        public virtual Task ProcessAsync() => Task.CompletedTask;
        public virtual HttpRequestMessage CreateRequestMessage() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");
    }
}
