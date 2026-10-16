using Core.Abstractions;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Processors
{
    /// <summary>
    /// Base for profile management processors (list/show/set/remove). These commands operate
    /// without requiring a resolved <see cref="Profile"/> on the context.
    /// </summary>
    public abstract class ProfileProcessorBase : IProcessor
    {
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventPublisher Publisher = null!;
        protected OutputStreamDelegate WriteOutput = null!;

        protected virtual void BeforeInit() { }
        protected virtual void AfterInit() { }

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor Init(ProcessorContext context)
        {
            BeforeInit();

            HttpPipeline = context.HttpPipeline;
            Publisher = context.Publisher;
            WriteOutput = context.OutputHandler.WriteOutput;

            AfterInit();
            return this;
        }

        public virtual Task ProcessAsync() => Task.CompletedTask;
    }
}
