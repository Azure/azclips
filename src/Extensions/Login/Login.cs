using Core.Abstractions;
using Core.Engine;
using Microsoft.Azure.Cirrus.Extensions.Resources.Client;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;
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

            var authService = ServiceProviderAsync.GetAwaiter().GetResult()
                .GetRequiredService<IAuthService>();
                // // todo: tenant ID should be got from context when it's implemented
                // .AcquireTokenSilentlyAsync(scopes: null, tenantId: null, promptOnError: true).GetAwaiter().GetResult();
            AccessToken = authService.LoginAsync(loginRequest).GetAwaiter().GetResult();
            // return new ResourcesClient().CreateOrUpdateResourceGroupAsync(SubscriptionId, Name, new ResourceGroup("eastus", Name), AccessToken).GetAwaiter().GetResult();
            return authService.GetCurrentContext();
        }

        public string Process(bool Serialize)
        {
            // TODO: models to provide model.ToJson() method for serialization.
            Models.ResourceGroup result = (Models.ResourceGroup)Process();
            if (Serialize)
            {
                return System.Text.Json.JsonSerializer.Serialize<Models.ResourceGroup>(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                throw new NotImplementedException("Non-serialized output is not implemented.");
            }
        }
    }
}
