// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class NewNetworkInterfaceProcessor_Orchestrator : ComputeProcessorBase<NetworkInterface, NetworkInterface>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VirtualNetworkName { get; set; } = string.Empty;
    public string NetworkSecurityGroupName { get; set; } = string.Empty;
    public List<string> SubnetNames { get; set; } = [];
    public List<List<string>> SubnetAddressPrefixes { get; set; } = [];
    public List<string> AddressPrefixes { get; set; } = [];
    public List<string> RuleNames { get; set; } = [];
    public List<string> RuleDirections { get; set; } = [];
    public List<string> RuleProtocols { get; set; } = [];
    public List<int> RulePriorities { get; set; } = [];
    public List<string> RuleSourceAddressPrefixes { get; set; } = [];
    public List<string> RuleSourcePortRanges { get; set; } = [];
    public List<string> RuleDestinationPortRanges { get; set; } = [];
    public List<string> RuleDestinationAddressPrefixes { get; set; } = [];
    public List<string> RuleAccesses { get; set; } = [];
    public bool AcceleratedNetworking { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
         NewVirtualNetworkProcessor vnetProcessor = CreateExtendedProcessor<NewVirtualNetworkProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = VirtualNetworkName;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.SubnetNames = SubnetNames;
            p.SubnetAddressPrefixes = SubnetAddressPrefixes;
            p.AddressPrefixes = AddressPrefixes;
        });

        NewNetworkSecurityGroupProcessor nsgProcessor = CreateExtendedProcessor<NewNetworkSecurityGroupProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = NetworkSecurityGroupName;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.RuleNames = RuleNames;
            p.RuleDirections = RuleDirections;
            p.RuleProtocols = RuleProtocols;
            p.RulePriorities = RulePriorities;
            p.RuleSourceAddressPrefixes = RuleSourceAddressPrefixes;
            p.RuleSourcePortRanges = RuleSourcePortRanges;
            p.RuleDestinationPortRanges = RuleDestinationPortRanges;
            p.RuleDestinationAddressPrefixes = RuleDestinationAddressPrefixes;
            p.RuleAccesses = RuleAccesses;
        });

        Task<HttpResponseMessage> vnetResponseTask = vnetProcessor.SendAsync();
        Task<HttpResponseMessage> nsgResponseTask = nsgProcessor.SendAsync();
        using HttpResponseMessage vnetResponse = await vnetResponseTask;
        using HttpResponseMessage nsgResponse = await nsgResponseTask;
        VirtualNetwork vnet = (await vnetResponse.Content.ReadFromJsonAsync<VirtualNetwork>(JsonOptions))!;
        NetworkSecurityGroup nsg = (await nsgResponse.Content.ReadFromJsonAsync<NetworkSecurityGroup>(JsonOptions))!;

        NewNetworkInterfaceProcessor nicProcessor = CreateExtendedProcessor<NewNetworkInterfaceProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = Name;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.SubnetIds = vnet.Properties.Subnets.Select(subnet => subnet.Id).ToList();
            p.NetworkSecurityGroupId = nsg.Id!;
            p.AcceleratedNetworking = AcceleratedNetworking;
        });
        return await nicProcessor.SendAsync();
    }
}