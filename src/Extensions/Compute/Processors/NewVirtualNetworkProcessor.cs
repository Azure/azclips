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

    public override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await EventListener.Signal(Events.ProcessorBeforeHttpRequestCreate);
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{Name}?api-version=2025-05-01";
        VirtualNetwork body = new VirtualNetwork
        {
            Location = Location,
            Properties = new VirtualNetworkProperties
            {
                AddressSpace = new AddressSpace
                {
                    AddressPrefixes = AddressPrefixes,
                    IpamPoolPrefixAllocations = []
                },
                Subnets = SubnetNames.Select((name, index) =>
                {
                    var subnetPrefixes = SubnetAddressPrefixes.ElementAtOrDefault(index) ?? [];
                    return new Subnet
                    {
                        Name = name,
                        Properties = new SubnetProperties
                        {
                            AddressPrefix = subnetPrefixes.FirstOrDefault() ?? string.Empty,
                            AddressPrefixes = subnetPrefixes,
                            ServiceEndpoints = [],
                            ServiceEndpointPolicies = [],
                            IpAllocations = [],
                            Delegations = [],
                            PrivateEndpointNetworkPolicies = "Disabled",
                            PrivateLinkServiceNetworkPolicies = "Enabled",
                            ApplicationGatewayIPConfigurations = [],
                            IpamPoolPrefixAllocations = []
                        }
                    };
                }).ToList(),
                VirtualNetworkPeerings = [],
                EnableDdosProtection = false,
                IpAllocations = []
            }
        };
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        await EventListener.Signal(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    /*
        Yabo:
        swagger says:         
        "x-ms-long-running-operation-options": {
          "final-state-via": "azure-async-operation",
          "final-state-schema": "#/definitions/Common.VirtualNetwork"
        }
        but got nothing, fallback to no finaluri
    */
    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => null;
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    public override async Task ProcessAsync()
    {
        await EventListener.Signal(Events.ProcessorProcessAsyncStart);
        SubscriptionId = !string.IsNullOrEmpty(SubscriptionId) ? SubscriptionId : AuthContext.SubscriptionId ?? throw new InvalidOperationException($"SubscriptionId must be provided either via property or auth context.");
        using HttpResponseMessage response = await SendAsyncLRO();
        VirtualNetwork body = (await response.Content.ReadFromJsonAsync<VirtualNetwork>(JsonOptions))!;
        await WriteOutput(body);
        await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
    }
}