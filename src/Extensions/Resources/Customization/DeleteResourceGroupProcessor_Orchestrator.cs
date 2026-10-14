// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Resolves client-specific resource group inputs.</summary>
public class DeleteResourceGroupProcessor_Orchestrator : DeleteResourceGroupProcessor
{
    public string? Id { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        bool hasName = !string.IsNullOrWhiteSpace(ResourceGroupName);
        bool hasId = !string.IsNullOrWhiteSpace(Id);
        if (hasName == hasId)
        {
            throw AzclipsErrorException.InvalidInput("Specify exactly one of resource group name or resource group id.");
        }

        if (hasId)
        {
            (SubscriptionId, ResourceGroupName) = ResourceGroupIdResolver.Parse(Id!);
        }

        await base.BeforeProcessAsync();
    }
}
