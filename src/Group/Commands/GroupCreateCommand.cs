using Core.Abstractions;
using Group.Business;
using Group.Business.Models;

namespace Group.Commands;

/// <summary>
/// CLI adapter for group create command.
/// Translates CLI options dictionary to business request and formats output.
/// </summary>
public class GroupCreateCommand : ICommand
{
    public async Task ExecuteAsync(IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        // Translate CLI options to business request
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

        Dictionary<string, string>? tags = null;
        if (options.TryGetValue("tags", out var tagsString) && !string.IsNullOrEmpty(tagsString))
        {
            tags = new Dictionary<string, string>();
            var tagPairs = tagsString.Split(',');
            foreach (var pair in tagPairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2)
                {
                    tags.Add(parts[0].Trim(), parts[1].Trim());
                }
            }
        }

        var request = new CreateGroupRequest
        {
            Name = name,
            Location = location,
            Tags = tags
        };

        try
        {
            Console.WriteLine($"Creating resource group '{name}' in location '{location}'...");
            
            // Call business logic
            var result = await GroupManager.CreateGroupAsync(request, cancellationToken);
            
            // Format output for CLI
            Console.WriteLine($"Resource group '{result.Name}' created successfully.");
            Console.WriteLine($"  ID: {result.Id}");
            Console.WriteLine($"  Location: {result.Location}");
            Console.WriteLine($"  Provisioning State: {result.ProvisioningState}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to create resource group: {ex.Message}");
            throw;
        }
    }
}
