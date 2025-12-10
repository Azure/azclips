using Core.Abstractions;
using Group.Business;
using Group.Business.Models;

namespace Group.Commands;

/// <summary>
/// CLI adapter for group delete command.
/// Translates CLI options dictionary to business request and formats output.
/// </summary>
public class GroupDeleteCommand : ICommand
{
    public async Task ExecuteAsync(IServiceProvider serviceProvider,IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        // Translate CLI options to business request
        if (!options.TryGetValue("name", out var name) || string.IsNullOrEmpty(name))
        {
            Console.Error.WriteLine("Resource group name is required. Use --name parameter.");
            throw new ArgumentException("name is required");
        }

        var request = new DeleteGroupRequest
        {
            Name = name
        };

        try
        {
            Console.WriteLine($"Deleting resource group '{name}'...");
            
            // Call business logic
            var deleted = await GroupManager.DeleteGroupAsync(request, cancellationToken);
            
            // Format output for CLI
            if (!deleted)
            {
                Console.WriteLine($"Resource group '{name}' not found.");
            }
            else
            {
                Console.WriteLine($"Resource group '{name}' deleted successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to delete resource group: {ex.Message}");
            throw;
        }
    }
}
