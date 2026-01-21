using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class LoginCommandBase : IProcessor, IEventListener
    {
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected readonly JsonSerializerOptions _jsonOptions = null!;
        protected CommandOutputDelegate WriteOutput = null!;
        public StartActivityDelegate StartActivity { get; set; } = null!;

        protected virtual void BeforeInit() { }
        protected virtual void AfterInit() { }

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor Init(ICommandContext context)
        {
            BeforeInit();

            HttpPipeline = context.HttpPipeline;
            WriteOutput = context.CommandOutputHandler.WriteOutput;
            StartActivity = context.CommandTelemetryHandler.StartActivity;

            AfterInit();
            return this;
        }

        public virtual Task ProcessAsync() => Task.CompletedTask;

        public Task Signal(string id, CancellationToken token, Func<object> createMessage) => throw new NotImplementedException();
    }
}