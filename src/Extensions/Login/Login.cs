using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class LoginCommand : IProcessor
    {
        private ICommandContext _commandContext = null!;

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

        public object Process()
        {
            var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
            var telemetryService = serviceProvider.GetRequiredService<ITelemetryService>();
            using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(LoginCommand)}.{nameof(Process)}");
            var authService = serviceProvider.GetRequiredService<IAuthService>();
            var authContextService = serviceProvider.GetRequiredService<IAuthContextService>();

            LoginRequest loginRequest = new()
            {
                TenantId = TenantId,
                SubscriptionId = SubscriptionId,
                UseDeviceCodeFlow = UseDeviceCode
            };

            var authResult = authService.LoginAsync(loginRequest).GetAwaiter().GetResult();
            AuthContext authContext = new()
            {
                SubscriptionId = SubscriptionId,
                TenantId = authResult.TenantId,
            };
            authContextService.SaveContextAsync(authContext).GetAwaiter().GetResult();
            return authContext;
        }

        public string Process(bool Serialize)
        {
            // TODO: models to provide model.ToJson() method for serialization.
            AuthContext result = (AuthContext)Process();
            if (Serialize)
            {
                return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                throw new NotImplementedException("Non-serialized output is not implemented.");
            }
        }

        public Task ProcessAsync() => throw new NotImplementedException();

        public IProcessor SetCommandContext(ICommandContext context)
        {
            _commandContext = context;
            return this;
        }
    }
}
