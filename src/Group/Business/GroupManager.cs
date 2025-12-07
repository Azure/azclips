using Group.Business.Models;

namespace Group.Business;

/// <summary>
/// Core business logic for Azure Resource Group operations.
/// No knowledge of CLI or PowerShell - just pure business operations.
/// MOCKUP: Returns simulated responses without actual Azure SDK calls.
/// </summary>
public static class GroupManager
{
    public static Task<GroupInfo> CreateGroupAsync(CreateGroupRequest request, CancellationToken cancellationToken = default)
    {
        // MOCKUP: Simulate successful resource group creation
        var result = new GroupInfo
        {
            Name = request.Name,
            Id = $"/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/{request.Name}",
            Location = request.Location,
            ProvisioningState = "Succeeded",
            ManagedBy = null,
            Tags = request.Tags ?? new Dictionary<string, string>()
        };

        return Task.FromResult(result);
    }

    public static Task<List<GroupInfo>> ListGroupsAsync(ListGroupsRequest request, CancellationToken cancellationToken = default)
    {
        // MOCKUP: Return simulated resource groups
        var results = new List<GroupInfo>();

        if (!string.IsNullOrEmpty(request.Name))
        {
            // Return single resource group
            results.Add(new GroupInfo
            {
                Name = request.Name,
                Id = $"/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/{request.Name}",
                Location = "eastus",
                ProvisioningState = "Succeeded",
                ManagedBy = null,
                Tags = new Dictionary<string, string> { { "Environment", "Demo" } }
            });
        }
        else
        {
            // Return multiple resource groups
            results.Add(new GroupInfo
            {
                Name = "rg-demo-001",
                Id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-demo-001",
                Location = "eastus",
                ProvisioningState = "Succeeded",
                Tags = new Dictionary<string, string> { { "Environment", "Demo" } }
            });

            results.Add(new GroupInfo
            {
                Name = "rg-demo-002",
                Id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-demo-002",
                Location = "westus",
                ProvisioningState = "Succeeded",
                Tags = new Dictionary<string, string> { { "Environment", "Test" } }
            });
        }

        return Task.FromResult(results);
    }

    public static Task<GroupInfo?> ShowGroupAsync(ShowGroupRequest request, CancellationToken cancellationToken = default)
    {
        // MOCKUP: Return simulated resource group details
        var result = new GroupInfo
        {
            Name = request.Name,
            Id = $"/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/{request.Name}",
            Location = "eastus",
            ProvisioningState = "Succeeded",
            ManagedBy = null,
            Tags = new Dictionary<string, string>
            {
                { "Environment", "Production" },
                { "CostCenter", "Engineering" }
            }
        };

        return Task.FromResult<GroupInfo?>(result);
    }

    public static Task<bool> DeleteGroupAsync(DeleteGroupRequest request, CancellationToken cancellationToken = default)
    {
        // MOCKUP: Simulate successful deletion
        return Task.FromResult(true);
    }
}
