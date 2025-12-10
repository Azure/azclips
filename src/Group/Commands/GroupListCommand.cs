using Core.Abstractions;
using Group.Business;
using Group.Business.Models;

namespace Group.Commands;

/// <summary>
/// CLI adapter for group list command.
/// Translates CLI options dictionary to business request and formats output.
/// </summary>
public class GroupListCommand : ICommand
{
    public async Task ExecuteAsync(IServiceProvider serviceProvider, IDictionary<string, string> options, CancellationToken cancellationToken = default)
    {
        // Translate CLI options to business request
        var request = new ListGroupsRequest
        {
            Name = options.ContainsKey("name") ? options["name"] : null
        };

        try
        {
            // Call business logic
            var results = await GroupManager.ListGroupsAsync(request, cancellationToken);
            
            // Format output for CLI
            if (results.Count == 0)
            {
                if (!string.IsNullOrEmpty(request.Name))
                {
                    Console.WriteLine($"Resource group '{request.Name}' not found.");
                }
                else
                {
                    Console.WriteLine("No resource groups found.");
                }
                return;
            }

            if (results.Count == 1 && !string.IsNullOrEmpty(request.Name))
            {
                // Show detailed view for single resource group
                var group = results[0];
                Console.WriteLine($"Resource Group: {group.Name}");
                Console.WriteLine($"  ID: {group.Id}");
                Console.WriteLine($"  Location: {group.Location}");
                Console.WriteLine($"  Provisioning State: {group.ProvisioningState}");
                if (group.Tags.Count > 0)
                {
                    Console.WriteLine($"  Tags:");
                    foreach (var tag in group.Tags)
                    {
                        Console.WriteLine($"    {tag.Key}: {tag.Value}");
                    }
                }
            }
            else
            {
                // List view for multiple resource groups
                Console.WriteLine("Resource Groups:");
                Console.WriteLine(new string('-', 80));
                foreach (var group in results)
                {
                    Console.WriteLine($"  {group.Name}");
                    Console.WriteLine($"    Location: {group.Location}");
                    Console.WriteLine($"    Provisioning State: {group.ProvisioningState}");
                    if (group.Tags.Count > 0)
                    {
                        Console.WriteLine($"    Tags: {string.Join(", ", group.Tags.Select(t => $"{t.Key}={t.Value}"))}");
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
}
