// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
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
    public bool AcceleratedNetworking { get; set; }

    /// <summary>OS type of the VM's image, which picks the default rule of a new NSG.</summary>
    public string OsType { get; set; } = string.Empty;

    public string? PublicIpAddress { get; set; }

    /// <summary>The public IP created when <see cref="PublicIpAddress"/> is null. Empty means none.</summary>
    public string DefaultPublicIpAddressName { get; set; } = string.Empty;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        // Explicit names and ids are looked up before anything is created, as legacy CLI checks
        // existence before it deploys, so a missing id fails without leaving a VNet or NSG behind.
        (string? existingPublicIpAddressId, string? publicIpAddressNameToCreate) = await NewOrExistingResourceResolver.ResolveAsync(
            PublicIpAddress, DefaultPublicIpAddressName, SubscriptionId, ResourceGroupName,
            NewOrExistingResourceResolver.PublicIpAddress, FindPublicIpAddressIdAsync);

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

        SecurityRule defaultRule = NetworkSecurityGroupDefaultRule.For(OsType);
        NewNetworkSecurityGroupProcessor nsgProcessor = CreateExtendedProcessor<NewNetworkSecurityGroupProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = NetworkSecurityGroupName;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.RuleNames = [defaultRule.Name];
            p.RuleDirections = [defaultRule.Properties.Direction];
            p.RuleProtocols = [defaultRule.Properties.Protocol];
            p.RulePriorities = [defaultRule.Properties.Priority];
            p.RuleSourceAddressPrefixes = [defaultRule.Properties.SourceAddressPrefix];
            p.RuleSourcePortRanges = [defaultRule.Properties.SourcePortRange];
            p.RuleDestinationPortRanges = [defaultRule.Properties.DestinationPortRange];
            p.RuleDestinationAddressPrefixes = [defaultRule.Properties.DestinationAddressPrefix];
            p.RuleAccesses = [defaultRule.Properties.Access];
        });

        Task<HttpResponseMessage> vnetResponseTask = vnetProcessor.SendAsync();
        Task<HttpResponseMessage> nsgResponseTask = nsgProcessor.SendAsync();
        Task<string?> publicIpAddressIdTask = publicIpAddressNameToCreate is null
            ? Task.FromResult(existingPublicIpAddressId)
            : CreatePublicIpAddressAsync(publicIpAddressNameToCreate);
        using HttpResponseMessage vnetResponse = await vnetResponseTask;
        using HttpResponseMessage nsgResponse = await nsgResponseTask;
        string? publicIpAddressId = await publicIpAddressIdTask;
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
            p.PublicIpAddressId = publicIpAddressId ?? string.Empty;
        });
        return await nicProcessor.SendAsync();
    }

    private async Task<string?> FindPublicIpAddressIdAsync(string subscriptionId, string resourceGroupName, string name)
    {
        using HttpResponseMessage response = await CreateExtendedProcessor<GetPublicIPAddressProcessor>(p =>
        {
            p.SubscriptionId = subscriptionId;
            p.ResourceGroupName = resourceGroupName;
            p.Name = name;
        }).SendAllowingNotFoundAsync();

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        PublicIPAddress publicIp = (await response.Content.ReadFromJsonAsync<PublicIPAddress>(JsonOptions))!;
        return publicIp.Id;
    }

    private async Task<string?> CreatePublicIpAddressAsync(string name)
    {
        using HttpResponseMessage response = await CreateExtendedProcessor<NewPublicIPAddressProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = name;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
        }).SendAsync();

        PublicIPAddress publicIp = (await response.Content.ReadFromJsonAsync<PublicIPAddress>(JsonOptions))!;
        return publicIp.Id;
    }
}
