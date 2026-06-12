using Core.Abstractions.Runtime;
// using Microsoft.Azure.Cirrus.ExtensionRuntime;
// using Microsoft.Azure.Cirrus.Extensions.Web.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Web.Processors;

public class DeleteWebappProcessor : WebProcessorBase<object>
{
	private const string apiVersion = "2024-11-01";

    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public bool? DeleteMetrics { get; set; } = null!;
    public bool? DeleteEmptyServerFarm { get; set; } = null!;

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

        string uri = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Web/sites/{Name}?";
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
