// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors
{
    /// <summary>
    /// Pure transport for <c>PUT .../storageAccounts/{accountName}</c>: it serializes the
    /// already-built <see cref="StorageAccountCreateParameters"/> and waits out the long-running
    /// operation, doing no validation or argument mapping of its own. All of that (and the full
    /// user-facing argument surface) lives in <see cref="NewStorageAccountProcessor_Orchestrator"/>,
    /// which builds the body and delegates here via <c>CreateExtendedProcessor</c>.
    /// </summary>
    public class NewStorageAccountProcessor : StorageProcessorBase<StorageAccountCreateParameters, StorageAccount>
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string ResourceGroupName { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;

        public StorageAccountCreateParameters Body { get; set; } = new();

        protected override HttpMethod Method => HttpMethod.Put;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts/{AccountName}?api-version=2026-04-01";

        protected override StorageAccountCreateParameters? CreateRequestBody() => Body;

        protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
        protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
        protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
        protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

        internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
    }
}
