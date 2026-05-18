using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Compute.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors;

public class DeleteNetworkInterfaceProcessor : ComputeProcessorBase<object>
{
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    public override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await EventListener.Signal(Events.ProcessorBeforeHttpRequestCreate);
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces/{Name}?api-version=2025-05-01";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, uri);
        await EventListener.Signal(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    protected override Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    public override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}