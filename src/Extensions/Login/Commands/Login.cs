using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    [CommandInfo(Name = "", Description = "Log in to Azure.")]
    public class LoginCommand : LoginCommandBase
    {
        private readonly IAuthService _authService;
        private readonly IAuthContextService _authContextService;

        public LoginCommand(
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

        [ArgumentInfo(
        Required = true,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string SubscriptionId { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Tenant ID to login to.",
        SerializedName = @"TenantId")]
        public string TenantId { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Use device code flow for authentication.",
        SerializedName = @"UseDeviceCode")]
        public bool UseDeviceCode { get; set; } = false;

        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(LoginCommand)}.{nameof(ProcessAsync)}");

            LoginRequest loginRequest = new()
            {
                TenantId = TenantId,
                SubscriptionId = SubscriptionId,
                UseDeviceCodeFlow = UseDeviceCode
            };

            var authResult = _authService.LoginAsync(loginRequest).GetAwaiter().GetResult();
            AuthContext authContext = new()
            {
                SubscriptionId = SubscriptionId,
                TenantId = authResult.TenantId,
            };
            _authContextService.SaveContextAsync(authContext).GetAwaiter().GetResult();
            await WriteOutput(authContext);
        }
    }
}
