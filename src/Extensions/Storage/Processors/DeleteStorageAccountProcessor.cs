// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors
{
    /// <summary>
    /// Deletes a storage account (<c>StorageAccounts_Delete</c>). A plain passthrough, matching
    /// azure-cli, which registers delete as <c>g.command('delete', 'delete')</c> with no custom
    /// code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not a long-running operation: the spec marks it <c>x-ms-long-running-operation: false</c>,
    /// and azure-cli binds the synchronous <c>delete</c> rather than a <c>begin_delete</c>. So it
    /// uses the plain send, not the polling one. The service answers 200 or 204 with no body, which
    /// the base pipeline reports as an empty response and writes nothing.
    /// </para>
    /// <para>
    /// azure-cli prompts for confirmation here (<c>confirmation=True</c>). There is no equivalent
    /// in azclips: no <c>--yes</c> flag exists, and ProcessorContext gives a processor no way to
    /// read from the console, so this cannot be honoured below the engine. PowerShell is unaffected
    /// because the cmdlet declares ShouldProcess with a high confirm impact.
    /// </para>
    /// </remarks>
    public class DeleteStorageAccountProcessor : StorageProcessorBase<object, object>
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string ResourceGroupName { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;

        protected override HttpMethod Method => HttpMethod.Delete;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts/{AccountName}?api-version=2026-04-01";
    }
}
