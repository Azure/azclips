using Core.Abstractions;
using Core.Engine;
using Microsoft.Azure.Cirrus.Extensions.Resources.Client;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Commands
{
    public class NewResourceGroupCommand : IProcessor
    {
        [ArgumentInfoAttribute(
        Required = true,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string SubscriptionId { get; set; } = "";

        [ArgumentInfoAttribute(
        Required = true,
        Description = @"The name of the resource group.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        [ArgumentInfoAttribute(
        Required = false,
        Description = @"The resource id of the resource group.",
        SerializedName = @"ResourceId")]
        public string ResourceId { get; set; } = "";

        [ArgumentInfoAttribute(
        Required = true,
        Description = @"Location for resource group.",
        SerializedName = @"Location")]
        public string Location { get; set; } = "";

        //public async Task ProcessAsync()
        //{

        //}

        public object Process()
        {
            var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
            var authService = serviceProvider.GetRequiredService<IAuthService>();
            var authContext = authService.GetCurrentContext();
            
            if (authContext == null || !authContext.IsAuthenticated)
            {
                throw new InvalidOperationException("Not authenticated. Please run 'azv2 login' first.");
            }
            
            var accessToken = authService
                .AcquireTokenSilentlyAsync(scopes: null, tenantId: authContext.TenantId, promptOnError: false).GetAwaiter().GetResult();
            return new ResourcesClient().CreateOrUpdateResourceGroupAsync(SubscriptionId, Name, new ResourceGroup("eastus", Name), accessToken).GetAwaiter().GetResult();
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
