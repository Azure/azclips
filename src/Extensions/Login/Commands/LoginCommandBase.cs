using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class LoginCommandBase : IProcessor
    {
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventListener EventListener = null!;
        protected readonly JsonSerializerOptions _jsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;

        protected virtual void BeforeInit() { }
        protected virtual void AfterInit() { }

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor Init(CommandContext context)
        {
            BeforeInit();

            HttpPipeline = context.HttpPipeline;
            EventListener = context.EventListener;
            WriteOutput = context.CommandOutputHandler.WriteOutput;

            AfterInit();
            return this;
        }

        public virtual Task ProcessAsync() => Task.CompletedTask;
    }
}