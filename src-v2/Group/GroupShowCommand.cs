using Azure.ResourceManager;
using Core.Abstractions;
using Login;

namespace Group;

public class GroupShowCommand : ICommand
{
    public async Task ExecuteAsync(IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        if (!AzureAuthenticationContext.IsAuthenticated)
        {
            Console.Error.WriteLine("Not authenticated. Run 'azv2 login' or 'Connect-AzV2Account' first.");
            throw new InvalidOperationException("Authentication required");
        }

        if (!options.TryGetValue("name", out var name) || string.IsNullOrEmpty(name))
        {
            Console.Error.WriteLine("Resource group name is required. Use --name parameter.");
            throw new ArgumentException("name is required");
        }

        var subscriptionId = AzureAuthenticationContext.SubscriptionId;
        if (string.IsNullOrEmpty(subscriptionId))
        {
            Console.Error.WriteLine("No subscription selected. Run 'azv2 login' first.");
            throw new InvalidOperationException("No subscription selected");
        }

        try
        {
            var armClient = new ArmClient(AzureAuthenticationContext.Credential);
            var subscription = armClient.GetSubscriptionResource(new Azure.Core.ResourceIdentifier($"/subscriptions/{subscriptionId}"));
            
            var resourceGroups = subscription.GetResourceGroups();
            
            var exists = await resourceGroups.ExistsAsync(name, cancellationToken);
            if (!exists.Value)
            {
                Console.WriteLine($"Resource group '{name}' not found.");
                return;
            }

            var resourceGroup = await resourceGroups.GetAsync(name, cancellationToken);
            
            Console.WriteLine($"Resource Group: {resourceGroup.Value.Data.Name}");
            Console.WriteLine($"  ID: {resourceGroup.Value.Id}");
            Console.WriteLine($"  Location: {resourceGroup.Value.Data.Location}");
            Console.WriteLine($"  Provisioning State: {resourceGroup.Value.Data.ProvisioningState}");
            Console.WriteLine($"  Managed By: {resourceGroup.Value.Data.ManagedBy ?? "None"}");
            
            if (resourceGroup.Value.Data.Tags.Count > 0)
            {
                Console.WriteLine($"  Tags:");
                foreach (var tag in resourceGroup.Value.Data.Tags)
                {
                    Console.WriteLine($"    {tag.Key}: {tag.Value}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to show resource group: {ex.Message}");
            throw;
        }
    }
}
