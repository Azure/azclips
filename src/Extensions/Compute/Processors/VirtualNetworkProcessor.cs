using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Extensions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Compute.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors;

public class NewVirtualNetworkProcessor : ComputeProcessorBase
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public List<string> SubnetNames { get; set; } = [];
    public List<List<string>> SubnetAddressPrefixes { get; set; } = [];
    public List<string> AddressPrefixes { get; set; } = [];

    public override async Task<HttpRequestMessage> CreateRequestMessage(string? subscriptionId = null, IEventListener? eventListener = null, JsonSerializerOptions? jsonOptions = null)
    {
        eventListener ??= EventListener;
        await eventListener.Signal(Events.ProcessorBeforeHttpRequestCreate);
        subscriptionId ??= SubscriptionId;
        jsonOptions ??= _jsonOptions;
        string uri = $"/subscriptions/{subscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{Name}?api-version=2025-05-01";
        VirtualNetwork body = new VirtualNetwork
        {
            Name = Name,
            Location = Location,
            ResourceGroupName = ResourceGroupName,
            Subnets = SubnetNames.Select((name, index) => new Subnet
            {
                Name = name,
                AddressPrefix = SubnetAddressPrefixes.ElementAtOrDefault(index) ?? new List<string>()
            }).ToList(),
            AddressSpace = new AddressSpace
            {
                AddressPrefixes = AddressPrefixes
            }
        };
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri)
        {
            Content = JsonContent.Create(body, options: jsonOptions)
        };
        await eventListener.Signal(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    public override async Task ProcessAsync()
    {
        await EventListener.Signal(Events.ProcessorProcessAsyncStart);
        SubscriptionId = SubscriptionId ?? AuthContext.SubscriptionId ?? throw new InvalidOperationException($"SubscriptionId must be provided either via property or auth context.");
        using HttpResponseMessage response = await SendAsyncLRO();
        await EventListener.Signal(Events.TracingHttpResponseMessage, response);
        VirtualNetwork body = (await response.Content.ReadFromJsonAsync<VirtualNetwork>(_jsonOptions))!;
        // TODO(Yabo): handle status codes
        await WriteOutput(body);
        await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
    }
}