using Core.Abstractions;
using Core.Engine;
using Microsoft.Azure.Cirrus.Extensions.Resources.Client;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Commands
{
    public class GetResourceGroupCommand : IProcessor
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string? SubscriptionId { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"The name of the resource group.",
        SerializedName = @"Name")]
        public string? Name { get; set; }

        public object Process()
        {
            var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
            var activitySource = serviceProvider.GetService<ITelemetryService>()?.ActivitySource;
            using var _ = activitySource?.StartActivity($"{nameof(GetResourceGroupCommand)}.{nameof(Process)}");

            var authService = serviceProvider.GetRequiredService<IAuthService>();
            var authContextService = serviceProvider.GetRequiredService<IAuthContextService>();
            var authContext = authContextService.GetCurrentContextAsync().GetAwaiter().GetResult();

            if (authContext == null)
            {
                throw new InvalidOperationException("Not authenticated. Please run 'azv2 login' first.");
            }

            var authResult = authService
                .AcquireTokenSilentlyAsync(scopes: null, tenantId: authContext.TenantId, promptOnError: false).GetAwaiter().GetResult();
            var client = new ResourcesClient() { ActivitySource = activitySource };
            return string.IsNullOrEmpty(Name) ? client.ListResourceGroupsAsync(SubscriptionId ?? authContext.SubscriptionId, authResult.AccessToken).GetAwaiter().GetResult()
                : client.GetResourceGroupAsync(SubscriptionId ?? authContext.SubscriptionId, Name, authResult.AccessToken).GetAwaiter().GetResult();
        }

        public string Process(bool Serialize)
        {
            if (Serialize)
            {
                if (string.IsNullOrEmpty(Name))
                {
                    IEnumerable<ResourceGroup> result = (IEnumerable<ResourceGroup>)Process();
                    return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    ResourceGroup result = (ResourceGroup)Process();
                    return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                }
            }
            else
            {
                throw new NotImplementedException("Non-serialized output is not implemented.");
            }
        }
    }
}
