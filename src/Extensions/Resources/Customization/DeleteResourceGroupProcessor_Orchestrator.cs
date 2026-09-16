// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Resolves client-specific resource group inputs.</summary>
public class DeleteResourceGroupProcessor_Orchestrator : DeleteResourceGroupProcessor
{
    public string? Id { get; set; }

    public bool Yes { get; set; } = true;

    protected override async Task BeforeProcessAsync()
    {
        if (!Yes)
        {
            throw new InvalidOperationException("--yes must be specified as a true switch value.");
        }

        bool hasName = !string.IsNullOrWhiteSpace(ResourceGroupName);
        bool hasId = !string.IsNullOrWhiteSpace(Id);
        if (hasName == hasId)
        {
            throw new InvalidOperationException("Specify exactly one of resource group name or resource group id.");
        }

        if (hasId)
        {
            (SubscriptionId, ResourceGroupName) = ResourceGroupIdResolver.Parse(Id!);
        }

        await base.BeforeProcessAsync();
    }
}
