// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors
{
    /// <summary>
    /// The customer-facing <c>az storage account list</c> / <c>Get-AzStorageAccount</c> (resource
    /// group form). Listing spans two API operations — one scoped to the subscription and one to a
    /// resource group — and nothing in the specification says a single command should choose
    /// between them, so that choice is made here rather than in a processor.
    /// </summary>
    /// <remarks>
    /// Mirrors azure-cli's <c>list_storage_accounts</c>, which branches the same way on whether a
    /// resource group was supplied.
    /// </remarks>
    public class ListStorageAccountProcessor_Customized : ListStorageAccountProcessor
    {
        /// <summary>Optional. Omit to list every storage account in the subscription.</summary>
        public string ResourceGroupName { get; set; } = string.Empty;

        // Delegates ProcessAsync rather than SendAsync so the child owns pagination and output;
        // CreateExtendedProcessor has already given it this processor's runtime context.
        public override async Task ProcessAsync()
        {
            if (string.IsNullOrEmpty(ResourceGroupName))
            {
                await base.ProcessAsync();
                return;
            }

            await CreateExtendedProcessor<ListStorageAccountByResourceGroupProcessor>(p =>
            {
                p.SubscriptionId = SubscriptionId;
                p.ResourceGroupName = ResourceGroupName;
            }).ProcessAsync();
        }
    }
}
