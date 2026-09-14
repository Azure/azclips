using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public partial class ListStorageAccountsCommand : StorageProcessorBase<object, StorageAccountListResult>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string? ResourceGroupName { get; set; }

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() => string.IsNullOrWhiteSpace(ResourceGroupName)
        ? $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version=2026-04-01"
        : $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version=2026-04-01";

    private IEnumerable<StorageAccount>? PageItems(StorageAccountListResult body) => body.Value;

    private string? NextLink(StorageAccountListResult body) => body.NextLink;

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(StorageAccountListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(PageItems, NextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
        return true;
    }
}