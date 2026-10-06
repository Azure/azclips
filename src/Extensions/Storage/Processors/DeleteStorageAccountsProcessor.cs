using System.Net.Http;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public partial class DeleteStorageAccountsProcessor : StorageProcessorBase<object, object>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Delete;

    protected override string CreateRequestUri() => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts/{AccountName}?api-version=2026-04-01";
}