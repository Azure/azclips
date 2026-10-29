// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors
{
    /// <summary>
    /// Lists every storage account in the subscription (<c>StorageAccounts_List</c>). Choosing
    /// between this and <see cref="ListStorageAccountByResourceGroupProcessor"/> is the caller's
    /// job; <see cref="ListStorageAccountProcessor_Orchestrator"/> does it from whether a resource
    /// group was supplied.
    /// </summary>
    public class ListStorageAccountProcessor : StorageProcessorBase<object, StorageAccountListResult>
    {
        public string SubscriptionId { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version=2026-04-01";

        private static string? nextLink(StorageAccountListResult body) => body.NextLink;
        private static StorageAccount[]? pageItems(StorageAccountListResult body) => body.Value;

        // The service pages this operation, so follow nextLink and write one flat array: PowerShell
        // enumerates the accounts and the CLI serializes them as a JSON array.
        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(StorageAccountListResult? body)
        {
            await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
            return true;
        }
    }
}
