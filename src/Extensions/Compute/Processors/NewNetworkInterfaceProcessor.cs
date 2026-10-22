// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class NewNetworkInterfaceProcessor : ComputeProcessorBase<NetworkInterface, NetworkInterface>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public List<string> SubnetIds { get; set; } = [];
    public List<List<string>> SubnetAddressPrefixes { get; set; } = [];
    public string NetworkSecurityGroupId { get; set; } = string.Empty;
    public bool AcceleratedNetworking { get; set; }

    /// <summary>Resource id of the public IP for the primary IP configuration. Empty attaches none.</summary>
    public string PublicIpAddressId { get; set; } = string.Empty;

    public Dictionary<string, string>? Tags { get; set; }

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces/{Name}?api-version=2025-05-01";

    protected override NetworkInterface? CreateRequestBody()
    {
        NetworkInterface body = new NetworkInterface
        {
            Location = Location,
            Tags = Tags,
            Properties = new NetworkInterfaceProperties
            {
                EnableAcceleratedNetworking = AcceleratedNetworking,
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
                        Primary = index == 0,
                        PublicIPAddress = index == 0 && !string.IsNullOrEmpty(PublicIpAddressId)
                            ? new ResourceReference { Id = PublicIpAddressId }
                            : null
                    }
                }).ToList()
            }
        };
        return body;
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

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}


