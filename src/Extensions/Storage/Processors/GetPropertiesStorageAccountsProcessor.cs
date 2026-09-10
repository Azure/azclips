using System.Net.Http;
using Microsoft.Azure.Azclips.Extensions.Storage.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public partial class GetPropertiesStorageAccountsProcessor : StorageProcessorBase<object, StorageAccount>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? Expand { get; set; }

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri()
    {
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts/{AccountName}?api-version=2026-04-01";
        return string.IsNullOrWhiteSpace(Expand) ? uri : $"{uri}&$expand={Uri.EscapeDataString(Expand)}";
    }
}
