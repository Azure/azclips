using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    [CommandInfo(Name = "logout", Description = "Log out of Azure.")]
    public class LogoutCommand : LoginCommandBase
    {
        private readonly IAuthService _authService;
        private readonly IAuthContextService _authContextService;

        public LogoutCommand(
            IRuntimeHttpPipeline httpPipeline,
            ICommandOutputHandler outputHandler,
            ICommandTelemetryHandler telemetryHandler,
            IAuthService authService,
            IAuthContextService authContextService)
            : base(httpPipeline, outputHandler, telemetryHandler)
        {
            _authService = authService;
            _authContextService = authContextService;
        }

        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(LogoutCommand)}.{nameof(ProcessAsync)}");
            _authService.LogoutAsync().GetAwaiter().GetResult();
            _authContextService.ClearContextAsync().GetAwaiter().GetResult();
            await WriteOutput(true);
        }
    }
}
