using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Commands
{
    public class ResourceCommandBase : IProcessor, IEventListener
    {
        protected IRuntimeHttpPipeline HttpPipeline;
        protected JsonSerializerOptions _jsonOptions;
        protected CommandOutputDelegate WriteOutput;
        public StartActivityDelegate StartActivity { get; set; }

        public ResourceCommandBase(
            IRuntimeHttpPipeline httpPipeline,
            ICommandOutputHandler outputHandler,
            ICommandTelemetryHandler telemetryHandler)
        {
            HttpPipeline = httpPipeline;
            WriteOutput = outputHandler.WriteOutput;
            StartActivity = telemetryHandler.StartActivity;

            // TODO: get json options from context/DI in the future
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public virtual Task ProcessAsync() => Task.CompletedTask;

        public Task Signal(string id, CancellationToken token, Func<object> createMessage) => throw new NotImplementedException();
    }
}