using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class LoginCommandBase : IProcessor, IEventListener
    {
        protected IRuntimeHttpPipeline HttpPipeline;
        protected readonly JsonSerializerOptions _jsonOptions = null!;
        protected CommandOutputDelegate WriteOutput;
        public StartActivityDelegate StartActivity { get; set; }

        public LoginCommandBase(
            IRuntimeHttpPipeline httpPipeline,
            ICommandOutputHandler outputHandler,
            ICommandTelemetryHandler telemetryHandler)
        {
            HttpPipeline = httpPipeline;
            WriteOutput = outputHandler.WriteOutput;
            StartActivity = telemetryHandler.StartActivity;
        }

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public virtual Task ProcessAsync() => Task.CompletedTask;

        public Task Signal(string id, CancellationToken token, Func<object> createMessage) => throw new NotImplementedException();
    }
}