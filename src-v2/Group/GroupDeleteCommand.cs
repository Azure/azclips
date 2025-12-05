using Azure.ResourceManager;
using Core.Abstractions;
using Login;

namespace Group;

public class GroupDeleteCommand : ICommand
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
            
            Console.WriteLine($"Deleting resource group '{name}'...");
            
            var operation = await resourceGroup.Value.DeleteAsync(
                Azure.WaitUntil.Completed,
                cancellationToken);

            Console.WriteLine($"Resource group '{name}' deleted successfully.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to delete resource group: {ex.Message}");
            throw;
        }
    }
}
