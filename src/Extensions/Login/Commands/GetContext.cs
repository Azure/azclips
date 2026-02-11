using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    [CommandInfo(Name = "show", Description = "Show current login context.")]
    public class GetContextCommand : LoginCommandBase
    {
        private readonly IAuthContextService _authContextService;

        public GetContextCommand(
            IRuntimeHttpPipeline httpPipeline,
            ICommandOutputHandler outputHandler,
            ICommandTelemetryHandler telemetryHandler,
            IAuthContextService authContextService)
            : base(httpPipeline, outputHandler, telemetryHandler)
        {
            _authContextService = authContextService;
        }

        public override async Task ProcessAsync() {
            using var _ = StartActivity($"{nameof(GetContextCommand)}.{nameof(ProcessAsync)}");
            AuthContext Context = _authContextService.GetCurrentContextAsync().GetAwaiter().GetResult()!;
            await WriteOutput(Context);
        }
    }
}
