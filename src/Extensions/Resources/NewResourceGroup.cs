using Core.Abstractions;
using Core.Engine;
using Microsoft.Azure.Cirrus.Extensions.Resources.Client;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;
using Microsoft.Extensions.DependencyInjection;
using SharedContracts;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Commands
{
    public class NewResourceGroupCommand : SharedContracts.IProcessor
    {
        [ArgumentInfoAttribute(
        Required = true,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string SubscriptionId { get; set; } = "";

        [ArgumentInfoAttribute(
        Required = true,
        Description = @"Temporarily enable access token for easy login.",
        SerializedName = @"AccessToken")]
        public string AccessToken { get; set; } = "";

        [ArgumentInfoAttribute(
        Required = true,
        Description = @"The name of the resource group.",
        SerializedName = @"ResourceGroupName")]
        public string Name { get; set; } = "";

        [ArgumentInfoAttribute(
        Required = true,
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
            AccessToken = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult()
                .GetRequiredService<IAuthService>()
                // todo: tenant ID should be got from context when it's implemented
                .AcquireTokenSilentlyAsync(scopes: null, tenantId: null, promptOnError: true).GetAwaiter().GetResult();
            return new ResourcesClient().CreateOrUpdateResourceGroupAsync(SubscriptionId, Name, new ResourceGroup("eastus", Name), AccessToken).GetAwaiter().GetResult();
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
