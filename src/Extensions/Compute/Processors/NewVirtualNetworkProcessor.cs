// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class NewVirtualNetworkProcessor : ComputeProcessorBase<VirtualNetwork, VirtualNetwork>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public List<string> SubnetNames { get; set; } = [];
    public List<List<string>> SubnetAddressPrefixes { get; set; } = [];
    public List<string> AddressPrefixes { get; set; } = [];
    public Dictionary<string, string>? Tags { get; set; }

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{Name}?api-version=2025-05-01";

    protected override VirtualNetwork? CreateRequestBody()
    {
        VirtualNetwork body = new VirtualNetwork
        {
            Location = Location,
            Tags = Tags,
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


