// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class CreateOrUpdateNetworkInterfacesProcessor_Orchestrator : ProcessorBase<NetworkInterface, NetworkInterface>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VirtualNetworkName { get; set; } = string.Empty;
    public string? SubnetName { get; set; }
    public List<string> SubnetNames { get; set; } = [];
    public List<List<string>> SubnetAddressPrefixes { get; set; } = [];
    public List<string> AddressPrefixes { get; set; } = [];
    public bool AcceleratedNetworking { get; set; }

    public string? NetworkSecurityGroup { get; set; }

    /// <summary>The NSG created when <see cref="NetworkSecurityGroup"/> is null. Empty means none.</summary>
    public string DefaultNetworkSecurityGroupName { get; set; } = string.Empty;

    /// <summary>OS type of the VM's image, which picks the default rule of a new NSG.</summary>
    public string OsType { get; set; } = string.Empty;

    public string? PublicIpAddress { get; set; }

    /// <summary>The public IP created when <see cref="PublicIpAddress"/> is null. Empty means none.</summary>
    public string DefaultPublicIpAddressName { get; set; } = string.Empty;

    /// <summary>Tags for the NIC, and for the VNet, NSG and public IP when this orchestrator creates them. Null sends none.</summary>
    public Dictionary<string, string>? Tags { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (SubnetName is not null && SubnetNames.Count > 0)
        {
            throw AzclipsErrorException.UsageError(
                "Specify either a single subnet or a list of subnet names, not both.");
        }

        // Explicit names and ids are looked up before anything is created, as legacy CLI checks
        // existence before it deploys, so a missing id fails without leaving a VNet or NSG behind.
        (string? existingNetworkSecurityGroupId, string? networkSecurityGroupNameToCreate) = await NewOrExistingResourceResolver.ResolveAsync(
            NetworkSecurityGroup, DefaultNetworkSecurityGroupName, SubscriptionId, ResourceGroupName,
            NewOrExistingResourceResolver.NetworkSecurityGroup, FindNetworkSecurityGroupIdAsync);
        (string? existingPublicIpAddressId, string? publicIpAddressNameToCreate) = await NewOrExistingResourceResolver.ResolveAsync(
            PublicIpAddress, DefaultPublicIpAddressName, SubscriptionId, ResourceGroupName,
            NewOrExistingResourceResolver.PublicIpAddress, FindPublicIpAddressIdAsync);

        Task<List<string>> subnetIdsTask = ResolveSubnetIdsAsync();
        List<string>? subnetIds = SubnetName is null ? null : await subnetIdsTask;
        Task<string?> networkSecurityGroupIdTask = networkSecurityGroupNameToCreate is null
            ? Task.FromResult(existingNetworkSecurityGroupId)
            : CreateNetworkSecurityGroupAsync(networkSecurityGroupNameToCreate);
        Task<string?> publicIpAddressIdTask = publicIpAddressNameToCreate is null
            ? Task.FromResult(existingPublicIpAddressId)
            : CreatePublicIpAddressAsync(publicIpAddressNameToCreate);
        subnetIds ??= await subnetIdsTask;
        string? networkSecurityGroupId = await networkSecurityGroupIdTask;
        string? publicIpAddressId = await publicIpAddressIdTask;

        CreateOrUpdateNetworkInterfacesProcessor nicProcessor = CreateExtendedProcessor<CreateOrUpdateNetworkInterfacesProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.NetworkInterfaceName = Name;
            p.ResourceGroupName = ResourceGroupName;
            p.Location = Location;
            p.Tags = Tags;
            p.EnableAcceleratedNetworking = AcceleratedNetworking;
            p.EnableIPForwarding = false;
            if (!string.IsNullOrEmpty(networkSecurityGroupId))
            {
                p.NetworkSecurityGroupId = networkSecurityGroupId;
            }

            p.IpConfigurations = subnetIds.Select((subnetId, index) =>
            {
                NetworkInterfaceIPConfiguration ipConfiguration = new()
                {
                    Name = $"ipconfig{index}",
                    SubnetId = subnetId,
                    PrivateIPAllocationMethod = "Dynamic",
                    Primary = index == 0,
                };
                if (index == 0 && !string.IsNullOrEmpty(publicIpAddressId))
                {
                    ipConfiguration.PublicIpAddressId = publicIpAddressId;
                }

                return ipConfiguration;
            }).ToList();
        });
        return await nicProcessor.SendAsync();
    }

    private async Task<List<string>> ResolveSubnetIdsAsync()
    {
        if (SubnetName is not null && SubnetName.Contains('/'))
        {
            (string subscriptionId, string resourceGroupName, string virtualNetworkName, string subnetName) = ParseSubnetResourceId(SubnetName);
            Subnet? subnet = await CreateExtendedProcessor<GetSubnetsProcessor>(p =>
            {
                p.SubscriptionId = subscriptionId;
                p.ResourceGroupName = resourceGroupName;
                p.VirtualNetworkName = virtualNetworkName;
                p.SubnetName = subnetName;
            }).GetOrDefaultAsync<Subnet>();
            if (subnet is null)
            {
                throw AzclipsErrorException.UsageError($"Subnet '{SubnetName}' was not found.");
            }

            return [$"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/virtualNetworks/{virtualNetworkName}/subnets/{subnetName}"];
        }

        if (string.IsNullOrWhiteSpace(VirtualNetworkName))
        {
            throw AzclipsErrorException.UsageError(
                SubnetName is null
                    ? "Specify a virtual network name when creating a network interface."
                    : "Specify a virtual network name when selecting a subnet by name.");
        }

        VirtualNetwork? existingVnet = await CreateExtendedProcessor<GetVirtualNetworksProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = ResourceGroupName;
            p.VirtualNetworkName = VirtualNetworkName;
        }).GetOrDefaultAsync<VirtualNetwork>();
        bool vnetExists = existingVnet is not null;
        if (existingVnet is not null)
        {
            IList<Subnet> existingSubnets = existingVnet.Subnets ?? [];
            if (SubnetName is not null)
            {
                Subnet? existingSubnet = existingSubnets.FirstOrDefault(
                    subnet => subnet.Name.Equals(SubnetName, StringComparison.OrdinalIgnoreCase));
                if (existingSubnet is null)
                {
                    throw AzclipsErrorException.UsageError(
                        $"Subnet '{SubnetName}' was not found in virtual network '{VirtualNetworkName}'.");
                }
                return [existingSubnet.Id!];
            }

            // No explicit network settings: reuse the existing VNet instead of overwriting it.
            if (SubnetNames.Count == 0 && AddressPrefixes.Count == 0 && SubnetAddressPrefixes.Count == 0)
            {
                Subnet? reusedSubnet = existingSubnets.FirstOrDefault(
                    subnet => subnet.Name.Equals("default", StringComparison.OrdinalIgnoreCase))
                    ?? existingSubnets.FirstOrDefault();
                if (reusedSubnet is null)
                {
                    throw AzclipsErrorException.UsageError(
                        $"Virtual network '{VirtualNetworkName}' has no subnets. Specify a subnet name.");
                }
                return [reusedSubnet.Id!];
            }
        }

        List<string> subnetNames = SubnetName is null ? SubnetNames : [SubnetName];
        List<string> addressPrefixes = AddressPrefixes;
        List<List<string>> subnetAddressPrefixes = [.. SubnetAddressPrefixes];

        // Defaults only apply when creating a new VNet, matching legacy `az vm create`.
        if (!vnetExists)
        {
            if (subnetNames.Count == 0)
            {
                subnetNames = ["default"];
            }

            if (AddressPrefixes.Count == 0)
            {
                addressPrefixes = ["10.0.0.0/16"];
            }

            if (subnetAddressPrefixes.Count == 0)
            {
                subnetAddressPrefixes.Add(["10.0.0.0/24"]);
            }
            else if (subnetAddressPrefixes[0].Count == 0)
            {
                subnetAddressPrefixes[0] = ["10.0.0.0/24"];
            }
        }

        CreateOrUpdateVirtualNetworksProcessor vnetProcessor = CreateExtendedProcessor<CreateOrUpdateVirtualNetworksProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.VirtualNetworkName = VirtualNetworkName;
            p.ResourceGroupName = ResourceGroupName;
            p.Location = Location;
            p.Tags = Tags;
            p.AddressSpaceAddressPrefixes = addressPrefixes;
            p.Subnets = subnetNames.Select((name, index) =>
            {
                List<string> subnetPrefixes = subnetAddressPrefixes.ElementAtOrDefault(index) ?? [];
                return new Subnet
                {
                    Name = name,
                    AddressPrefix = subnetPrefixes.FirstOrDefault(),
                    AddressPrefixes = subnetPrefixes,
                    PrivateEndpointNetworkPolicies = "Disabled",
                    PrivateLinkServiceNetworkPolicies = "Enabled",
                };
            }).ToList();
        });
        using HttpResponseMessage vnetResponse = await vnetProcessor.SendAsync();
        VirtualNetwork vnet = (await vnetResponse.Content.ReadFromJsonAsync<VirtualNetwork>(JsonOptions))!;
        return (vnet.Subnets ?? []).Select(subnet => subnet.Id!).ToList();
    }

    private static (string SubscriptionId, string ResourceGroupName, string VirtualNetworkName, string SubnetName) ParseSubnetResourceId(string id)
    {
        string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 10
            || !segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)
            || !segments[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase)
            || !segments[4].Equals("providers", StringComparison.OrdinalIgnoreCase)
            || !segments[5].Equals("Microsoft.Network", StringComparison.OrdinalIgnoreCase)
            || !segments[6].Equals("virtualNetworks", StringComparison.OrdinalIgnoreCase)
            || !segments[8].Equals("subnets", StringComparison.OrdinalIgnoreCase))
        {
            throw AzclipsErrorException.UsageError(
                $"Invalid subnet resource id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/Microsoft.Network/virtualNetworks/{{virtualNetworkName}}/subnets/{{subnetName}}.");
        }

        return (segments[1], segments[3], segments[7], segments[9]);
    }

    private async Task<string?> FindNetworkSecurityGroupIdAsync(string subscriptionId, string resourceGroupName, string name)
        => (await CreateExtendedProcessor<GetNetworkSecurityGroupsProcessor>(p =>
        {
            p.SubscriptionId = subscriptionId;
            p.ResourceGroupName = resourceGroupName;
            p.NetworkSecurityGroupName = name;
        }).GetOrDefaultAsync<NetworkSecurityGroup>())?.Id;

    private async Task<string?> FindPublicIpAddressIdAsync(string subscriptionId, string resourceGroupName, string name)
        => (await CreateExtendedProcessor<GetPublicIPAddressesProcessor>(p =>
        {
            p.SubscriptionId = subscriptionId;
            p.ResourceGroupName = resourceGroupName;
            p.PublicIpAddressName = name;
        }).GetOrDefaultAsync<PublicIPAddress>())?.Id;

    private async Task<string?> CreateNetworkSecurityGroupAsync(string name)
    {
        using HttpResponseMessage response = await CreateExtendedProcessor<CreateOrUpdateNetworkSecurityGroupsProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.NetworkSecurityGroupName = name;
            p.ResourceGroupName = ResourceGroupName;
            p.Location = Location;
            p.Tags = Tags;
            p.SecurityRules = [NetworkSecurityGroupDefaultRule.For(OsType)];
        }).SendAsync();

        NetworkSecurityGroup networkSecurityGroup = (await response.Content.ReadFromJsonAsync<NetworkSecurityGroup>(JsonOptions))!;
        return networkSecurityGroup.Id;
    }

    private async Task<string?> CreatePublicIpAddressAsync(string name)
    {
        using HttpResponseMessage response = await CreateExtendedProcessor<CreateOrUpdatePublicIPAddressesProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.PublicIpAddressName = name;
            p.ResourceGroupName = ResourceGroupName;
            p.Location = Location;
            p.Tags = Tags;
            p.SkuName = "Standard";
            p.PublicIPAllocationMethod = "Static";
        }).SendAsync();

        PublicIPAddress publicIp = (await response.Content.ReadFromJsonAsync<PublicIPAddress>(JsonOptions))!;
        return publicIp.Id;
    }
}
