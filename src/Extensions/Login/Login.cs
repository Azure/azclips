using Core.Abstractions;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;
using SharedContracts;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class LoginCommand : SharedContracts.IProcessor
    {
        [ArgumentInfoAttribute(
        Required = true,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string SubscriptionId { get; set; } = "";

        [ArgumentInfoAttribute(
        Required = true,
        Description = @"Tenant ID to login to.",
        SerializedName = @"TenantID")]
        public string TenantId { get; set; } = "";

        public object Process()
        {
            LoginRequest loginRequest = new LoginRequest()
            {
                TenantId = TenantId,
                SubscriptionId = SubscriptionId
            };

            var authService = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult()
                .GetRequiredService<IAuthService>();
            authService.LoginAsync(loginRequest).GetAwaiter().GetResult();
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
