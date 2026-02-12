using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Commands
{
    public class ResourceCommandBase : IProcessor, IEventListener
    {
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected JsonSerializerOptions _jsonOptions = null!;
        protected CommandOutputDelegate WriteOutput = null!;
        protected AuthContext AuthContext = null!;
        public StartActivityDelegate StartActivity { get; set; } = null!;

        protected virtual void BeforeInit() { }
        protected virtual void AfterInit() { }

        public ResourceCommandBase(
            IRuntimeHttpPipeline httpPipeline,
            ICommandOutputHandler outputHandler,
            ICommandTelemetryHandler telemetryHandler,
            ICommandAuthHandler authHandler)
        {
            BeforeInit();

            HttpPipeline = httpPipeline;
            WriteOutput = outputHandler.WriteOutput;
            StartActivity = telemetryHandler.StartActivity;
            AuthContext = authHandler.AuthContext;

            // TODO: get json options from context/DI in the future
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            AfterInit();
        }

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public virtual Task ProcessAsync() => Task.CompletedTask;

        public Task Signal(string id, CancellationToken token, Func<object> createMessage) => throw new NotImplementedException();
    }
}