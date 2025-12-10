using Core.Abstractions;
using Group.Business;
using Group.Business.Models;

namespace Group.Commands;

/// <summary>
/// CLI adapter for group show command.
/// Translates CLI options dictionary to business request and formats output.
/// </summary>
public class GroupShowCommand : ICommand
{
    public async Task ExecuteAsync(IServiceProvider serviceProvider, IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        // Translate CLI options to business request
        if (!options.TryGetValue("name", out var name) || string.IsNullOrEmpty(name))
        {
            Console.Error.WriteLine("Resource group name is required. Use --name parameter.");
            throw new ArgumentException("name is required");
        }

        var request = new ShowGroupRequest
        {
            Name = name
        };

        try
        {
            // Call business logic
            var result = await GroupManager.ShowGroupAsync(request, cancellationToken);
            
            // Format output for CLI
            if (result == null)
            {
                Console.WriteLine($"Resource group '{name}' not found.");
                return;
            }

            Console.WriteLine($"Resource Group: {result.Name}");
            Console.WriteLine($"  ID: {result.Id}");
            Console.WriteLine($"  Location: {result.Location}");
            Console.WriteLine($"  Provisioning State: {result.ProvisioningState}");
            Console.WriteLine($"  Managed By: {result.ManagedBy ?? "None"}");
            
            if (result.Tags.Count > 0)
            {
                Console.WriteLine($"  Tags:");
                foreach (var tag in result.Tags)
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
