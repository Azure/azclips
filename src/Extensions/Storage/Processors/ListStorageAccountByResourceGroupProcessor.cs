// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors
{
    /// <summary>
    /// Lists the storage accounts in one resource group
    /// (<c>StorageAccounts_ListByResourceGroup</c>).
    /// </summary>
    public class ListStorageAccountByResourceGroupProcessor : StorageProcessorBase<object, StorageAccountListResult>
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string ResourceGroupName { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version=2026-04-01";

        private static string? nextLink(StorageAccountListResult body) => body.NextLink;
        private static StorageAccount[]? pageItems(StorageAccountListResult body) => body.Value;

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(StorageAccountListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
