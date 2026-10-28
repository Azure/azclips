// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors
{
    /// <summary>
    /// Gets the properties of a storage account. A plain passthrough of
    /// <c>StorageAccounts_GetProperties</c>, matching azure-cli, which registers show as
    /// <c>g.show_command('show', 'get_properties')</c> with no custom code.
    /// </summary>
    /// <remarks>
    /// azure-cli also accepts <c>--ids</c> here, which fans a command out over several resource
    /// ids. That is a client-side convenience with no counterpart in the API, so it would have to
    /// live in a customization (see Identity's ShowIdentityProcessor_Orchestrator). It is not part
    /// of the storage account scope, so this processor stays a pure passthrough.
    /// </remarks>
    public class ShowStorageAccountProcessor : StorageProcessorBase<object, StorageAccount>
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string ResourceGroupName { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;

        /// <summary>
        /// Optional <c>$expand</c>. The service accepts <c>geoReplicationStats</c> and
        /// <c>blobRestoreStatus</c>; only the former is surfaced, since blob restore is not part of
        /// this extension.
        /// </summary>
        public string Expand { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri()
        {
            string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts/{AccountName}?api-version=2026-04-01";
            return string.IsNullOrEmpty(Expand) ? uri : $"{uri}&$expand={Expand}";
        }
    }
}
