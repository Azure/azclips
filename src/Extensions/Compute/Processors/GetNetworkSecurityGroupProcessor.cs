using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Compute.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors;

public class GetNetworkSecurityGroupProcessor : ComputeProcessorBase<NetworkSecurityGroup>
{
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    public override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkSecurityGroups/{Name}?api-version=2025-05-01";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}
