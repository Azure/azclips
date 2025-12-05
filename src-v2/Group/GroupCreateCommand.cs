using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Core.Abstractions;
using Login;

namespace Group;

public class GroupCreateCommand : ICommand
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

        if (!options.TryGetValue("location", out var location) || string.IsNullOrEmpty(location))
        {
            Console.Error.WriteLine("Location is required. Use --location parameter.");
            throw new ArgumentException("location is required");
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

            var data = new ResourceGroupData(location);
            
            // Parse tags if provided
            if (options.TryGetValue("tags", out var tagsString) && !string.IsNullOrEmpty(tagsString))
            {
                var tagPairs = tagsString.Split(',');
                foreach (var pair in tagPairs)
                {
                    var parts = pair.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        data.Tags.Add(parts[0].Trim(), parts[1].Trim());
                    }
                }
            }

            Console.WriteLine($"Creating resource group '{name}' in location '{location}'...");
            
            var operation = await resourceGroups.CreateOrUpdateAsync(
                Azure.WaitUntil.Completed,
                name,
                data,
                cancellationToken);

            var resourceGroup = operation.Value;
            Console.WriteLine($"Resource group '{resourceGroup.Data.Name}' created successfully.");
            Console.WriteLine($"  ID: {resourceGroup.Id}");
            Console.WriteLine($"  Location: {resourceGroup.Data.Location}");
            Console.WriteLine($"  Provisioning State: {resourceGroup.Data.ProvisioningState}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to create resource group: {ex.Message}");
            throw;
        }
    }
}
