// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// The customer-facing <c>az network public-ip list</c> / <c>Get-AzPublicIpAddress</c> (list form). Listing spans two API
/// operations, one scoped to the subscription and one to a resource group, so the choice between
/// them is made here rather than in a processor.
/// </summary>
public class ListAllPublicIPAddressesProcessor_Orchestrator : ListAllPublicIPAddressesProcessor
{
    /// <summary>Optional. Omit to list every public IP address in the subscription.</summary>
    public string ResourceGroupName { get; set; } = string.Empty;

    // Delegates ProcessAsync rather than SendAsync so the child owns pagination and output.
    public override async Task ProcessAsync()
    {
        if (string.IsNullOrEmpty(ResourceGroupName))
        {
            await base.ProcessAsync();
            return;
        }

        await CreateExtendedProcessor<ListPublicIPAddressesProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = ResourceGroupName;
        }).ProcessAsync();
    }
}
