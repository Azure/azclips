using System.Diagnostics;
using Core.Abstractions;
using Core.Engine;
using Microsoft.Azure.Cirrus.Extensions.Resources.Client;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Commands
{
    public class NewResourceGroupCommand : IProcessor
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string? SubscriptionId { get; set; }

        [ArgumentInfo(
        Required = true,
        Description = @"The name of the resource group.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"The resource id of the resource group.",
        SerializedName = @"ResourceId")]
        public string ResourceId { get; set; } = "";

        [ArgumentInfo(
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
            var activitySource = serviceProvider.GetService<ITelemetryService>()?.ActivitySource;
            using var _ = activitySource?.StartActivity($"{nameof(NewResourceGroupCommand)}.{nameof(Process)}");

            var authService = serviceProvider.GetRequiredService<IAuthService>();
            var authContextService = serviceProvider.GetRequiredService<IAuthContextService>();
            var authContext = authContextService.GetCurrentContextAsync().GetAwaiter().GetResult();

            if (authContext == null)
            {
                throw new InvalidOperationException("Not authenticated. Please run 'azv2 login' first.");
            }

            var authResult = authService
                .AcquireTokenSilentlyAsync(scopes: null, tenantId: authContext.TenantId, promptOnError: false).GetAwaiter().GetResult();
            return new ResourcesClient { ActivitySource = activitySource }.CreateOrUpdateResourceGroupAsync(SubscriptionId ?? authContext.SubscriptionId, Name, new ResourceGroup(Location, Name), authResult.AccessToken).GetAwaiter().GetResult();
        }

        public string Process(bool Serialize)
        {
            // TODO: models to provide model.ToJson() method for serialization.
            ResourceGroup result = (ResourceGroup)Process();
            if (Serialize)
            {
                return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                throw new NotImplementedException("Non-serialized output is not implemented.");
            }
        }
    }
}
