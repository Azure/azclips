// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class NewNetworkInterfaceProcessor_Orchestrator : ComputeProcessorBase<NetworkInterface, NetworkInterface>
{
    private const string DefaultAddressPrefix = "10.0.0.0/16";
    private const string DefaultSubnetAddressPrefix = "10.0.0.0/24";

    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VirtualNetworkName { get; set; } = string.Empty;
    public string NetworkSecurityGroupName { get; set; } = string.Empty;
    public string PublicIPAddressId { get; set; } = string.Empty;
    public string PublicIPAddressName { get; set; } = string.Empty;
    public string PublicIPAddressDomainNameLabel { get; set; } = string.Empty;
    public string PublicIPAddressSku { get; set; } = "Standard";
    public string PublicIPAddressAllocationMethod { get; set; } = "Static";
    public bool NoPublicIPAddress { get; set; }
    public bool? EnableAcceleratedNetworking { get; set; }
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

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        List<string> subnetNames = SubnetNames.Count > 0 ? SubnetNames : ["default"];
        List<List<string>> subnetAddressPrefixes = SubnetAddressPrefixes.Count > 0 ? SubnetAddressPrefixes : [[DefaultSubnetAddressPrefix]];
        List<string> addressPrefixes = AddressPrefixes.Count > 0 ? AddressPrefixes : [DefaultAddressPrefix];

         NewVirtualNetworkProcessor vnetProcessor = CreateExtendedProcessor<NewVirtualNetworkProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = VirtualNetworkName;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.SubnetNames = subnetNames;
            p.SubnetAddressPrefixes = subnetAddressPrefixes;
            p.AddressPrefixes = addressPrefixes;
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
        Task<HttpResponseMessage>? publicIpResponseTask = CreatePublicIPAddressResponseTask();
        using HttpResponseMessage vnetResponse = await vnetResponseTask;
        using HttpResponseMessage nsgResponse = await nsgResponseTask;
        VirtualNetwork vnet = (await vnetResponse.Content.ReadFromJsonAsync<VirtualNetwork>(JsonOptions))!;
        NetworkSecurityGroup nsg = (await nsgResponse.Content.ReadFromJsonAsync<NetworkSecurityGroup>(JsonOptions))!;

        string publicIPAddressId = NoPublicIPAddress ? string.Empty : PublicIPAddressId;
        if (publicIpResponseTask is not null)
        {
            using HttpResponseMessage publicIpResponse = await publicIpResponseTask;
            ArmResourceReference? publicIp = await publicIpResponse.Content.ReadFromJsonAsync<ArmResourceReference>(JsonOptions);
            publicIPAddressId = publicIp?.Id ?? string.Empty;
        }

        NewNetworkInterfaceProcessor_Customized nicProcessor = CreateExtendedProcessor<NewNetworkInterfaceProcessor_Customized>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = Name;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.SubnetIds = vnet.Properties.Subnets.Select(subnet => subnet.Id).ToList();
            p.NetworkSecurityGroupId = nsg.Id!;
            p.PublicIPAddressId = publicIPAddressId;
            p.EnableAcceleratedNetworking = EnableAcceleratedNetworking;
        });
        return await nicProcessor.SendAsync();
    }

    private Task<HttpResponseMessage>? CreatePublicIPAddressResponseTask()
    {
        if (NoPublicIPAddress || !string.IsNullOrWhiteSpace(PublicIPAddressId))
        {
            return null;
        }

        string publicIPAddressName = PublicIPAddressName;
        if (string.IsNullOrWhiteSpace(publicIPAddressName) && !string.IsNullOrWhiteSpace(PublicIPAddressDomainNameLabel))
        {
            publicIPAddressName = $"{Name}-ip";
        }

        if (string.IsNullOrWhiteSpace(publicIPAddressName))
        {
            return null;
        }

        NewPublicIPAddressProcessor_Orchestrator publicIpProcessor = CreateExtendedProcessor<NewPublicIPAddressProcessor_Orchestrator>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = publicIPAddressName;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.DomainNameLabel = PublicIPAddressDomainNameLabel;
            p.Sku = PublicIPAddressSku;
            p.AllocationMethod = PublicIPAddressAllocationMethod;
        });
        return publicIpProcessor.SendAsync();
    }
}