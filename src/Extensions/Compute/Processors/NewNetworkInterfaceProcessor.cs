using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Extensions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Compute.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors;

public class NewNetworkInterfaceProcessor : ComputeProcessorBase
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public List<string> SubnetIds { get; set; } = [];
    public List<List<string>> SubnetAddressPrefixes { get; set; } = [];
    public string NetworkSecurityGroupId { get; set; } = string.Empty;

    public override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await EventListener.Signal(Events.ProcessorBeforeHttpRequestCreate);
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces/{Name}?api-version=2025-05-01";
        NetworkInterface body = new NetworkInterface
        {
            Location = Location,
            Properties = new NetworkInterfaceProperties
            {
                EnableAcceleratedNetworking = true,
                EnableIPForwarding = false,
                NetworkSecurityGroup = string.IsNullOrEmpty(NetworkSecurityGroupId)
                    ? null
                    : new NetworkSecurityGroup
                    {
                        Id = NetworkSecurityGroupId,
                        Properties = new NetworkSecurityGroupProperties
                        {
                            FlushConnection = false,
                            SecurityRules = []
                        },
                    },
                IPConfigurations = SubnetIds.Select((subnetId, index) => new IPConfiguration
                {
                    Name = $"ipconfig{index}",
                    Properties = new IPConfigurationProperties
                    {
                        Subnet = new Subnet
                        {
                            Id = subnetId,
                            Properties = new SubnetProperties
                            {
                                AddressPrefixes = SubnetAddressPrefixes.ElementAtOrDefault(index) ?? [],
                                ServiceEndpoints = [],
                                ServiceEndpointPolicies = [],
                                IpAllocations = [],
                                Delegations = [],
                                ApplicationGatewayIPConfigurations = [],
                                IpamPoolPrefixAllocations = []
                            }
                        },
                        VirtualNetworkTaps = [],
                        ApplicationGatewayBackendAddressPools = [],
                        LoadBalancerBackendAddressPools = [],
                        LoadBalancerInboundNatRules = [],
                        PrivateIPAllocationMethod = "Dynamic",
                        Primary = index == 0
                    }
                }).ToList()
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
        NetworkInterface body = (await response.Content.ReadFromJsonAsync<NetworkInterface>(JsonOptions))!;
        await WriteOutput(body);
        await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
    }
}