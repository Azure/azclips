using Core.Abstractions;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class LoginCommand : IProcessor
    {
        [ArgumentInfoAttribute(
        Required = true,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string SubscriptionId { get; set; } = "";

        [ArgumentInfoAttribute(
        Required = true,
        Description = @"Tenant ID to login to.",
        SerializedName = @"TenantId")]
        public string TenantId { get; set; } = "";

        [ArgumentInfoAttribute(
        Required = false,
        Description = @"Use device code flow for authentication.",
        SerializedName = @"UseDeviceCode")]
        public bool UseDeviceCode { get; set; } = false;

        public object Process()
        {
            LoginRequest loginRequest = new LoginRequest()
            {
                TenantId = TenantId,
                SubscriptionId = SubscriptionId,
                UseDeviceCodeFlow = UseDeviceCode
            };


            var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
            var telemetryService = serviceProvider.GetRequiredService<ITelemetryService>();
            var authService = serviceProvider.GetRequiredService<IAuthService>();

            using (var activity = telemetryService.ActivitySource.StartActivity("LoginCommand.SendRequest"))
            {
                authService.LoginAsync(loginRequest).GetAwaiter().GetResult();
            }
            return authService.GetCurrentContext() ?? new AuthenticationInfo() { SubscriptionId = "", TenantId = "" };
        }

        public string Process(bool Serialize)
        {
            // TODO: models to provide model.ToJson() method for serialization.
            AuthenticationInfo result = (AuthenticationInfo)Process();
            if (Serialize)
            {
                return System.Text.Json.JsonSerializer.Serialize<AuthenticationInfo>(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                throw new NotImplementedException("Non-serialized output is not implemented.");
            }
        }
    }
}
