// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Resolves PowerShell resource group inputs for tag replacement.</summary>
public class SetResourceGroupProcessor_Orchestrator : UpdateResourceGroupProcessor
{
    public string? Id { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        bool hasName = !string.IsNullOrWhiteSpace(ResourceGroupName);
        bool hasId = !string.IsNullOrWhiteSpace(Id);
        if (hasName == hasId)
        {
            throw new InvalidOperationException("Specify exactly one of resource group name or resource group id.");
        }

        if (Tags is null)
        {
            throw new InvalidOperationException("The Tag parameter is required.");
        }

        if (hasId)
        {
            (SubscriptionId, ResourceGroupName) = ResourceGroupIdResolver.Parse(Id!);
        }

        await base.BeforeProcessAsync();
    }
}
