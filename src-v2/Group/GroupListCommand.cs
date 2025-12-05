using Azure.ResourceManager;
using Core.Abstractions;
using Login;

namespace Group;

public class GroupListCommand : ICommand
{
    public async Task ExecuteAsync(IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        if (!AzureAuthenticationContext.IsAuthenticated)
        {
            Console.Error.WriteLine("Not authenticated. Run 'azv2 login' or 'Connect-AzV2Account' first.");
            throw new InvalidOperationException("Authentication required");
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

            // If name is specified, show only that resource group
            if (options.TryGetValue("name", out var name) && !string.IsNullOrEmpty(name))
            {
                var exists = await resourceGroups.ExistsAsync(name, cancellationToken);
                if (!exists.Value)
                {
                    Console.WriteLine($"Resource group '{name}' not found.");
                    return;
                }

                var resourceGroup = await resourceGroups.GetAsync(name, cancellationToken);
                PrintResourceGroup(resourceGroup.Value);
            }
            else
            {
                // List all resource groups
                Console.WriteLine("Resource Groups:");
                Console.WriteLine(new string('-', 80));
                
                await foreach (var resourceGroup in resourceGroups.GetAllAsync(cancellationToken))
                {
                    Console.WriteLine($"  {resourceGroup.Data.Name}");
                    Console.WriteLine($"    Location: {resourceGroup.Data.Location}");
                    Console.WriteLine($"    Provisioning State: {resourceGroup.Data.ProvisioningState}");
                    if (resourceGroup.Data.Tags.Count > 0)
                    {
                        Console.WriteLine($"    Tags: {string.Join(", ", resourceGroup.Data.Tags.Select(t => $"{t.Key}={t.Value}"))}");
                    }
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to list resource groups: {ex.Message}");
            throw;
        }
    }

    private void PrintResourceGroup(Azure.ResourceManager.Resources.ResourceGroupResource resourceGroup)
    {
        Console.WriteLine($"Resource Group: {resourceGroup.Data.Name}");
        Console.WriteLine($"  ID: {resourceGroup.Id}");
        Console.WriteLine($"  Location: {resourceGroup.Data.Location}");
        Console.WriteLine($"  Provisioning State: {resourceGroup.Data.ProvisioningState}");
        if (resourceGroup.Data.Tags.Count > 0)
        {
            Console.WriteLine($"  Tags:");
            foreach (var tag in resourceGroup.Data.Tags)
            {
                Console.WriteLine($"    {tag.Key}: {tag.Value}");
            }
        }
    }
}
