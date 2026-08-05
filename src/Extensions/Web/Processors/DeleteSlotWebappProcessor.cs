using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Web.Processors;

public class DeleteSlotWebappProcessor : WebProcessorBase<object>
{
    private const string apiVersion = "2024-11-01";
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Slot { get; set; } = string.Empty;
    public bool? DeleteMetrics { get; set; } = null!;
    public bool? DeleteEmptyServerFarm { get; set; } = null!;

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

        string uri = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{Name}/slots/{Slot}?";
        if (DeleteMetrics.HasValue)
            uri += $"deleteMetrics={DeleteMetrics}&";
        if (DeleteEmptyServerFarm.HasValue)
            uri += $"deleteEmptyServerFarm={DeleteEmptyServerFarm}&";
        uri += $"api-version={apiVersion}";

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, uri);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}
