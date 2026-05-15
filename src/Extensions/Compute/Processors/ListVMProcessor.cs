using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Compute.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors;

public class ListVMProcessor : ComputeProcessorBase<VirtualMachine>
{
    public string ResourceGroupName { get; set; } = string.Empty;

    public override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await EventListener.Signal(Events.ProcessorBeforeHttpRequestCreate);
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines?api-version=2025-11-01";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
        await EventListener.Signal(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}