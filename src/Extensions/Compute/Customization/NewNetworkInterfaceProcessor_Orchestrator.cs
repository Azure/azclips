// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class NewNetworkInterfaceProcessor_Orchestrator : ComputeProcessorBase<NetworkInterface, NetworkInterface>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VirtualNetworkName { get; set; } = string.Empty;
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

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        // Explicit names and ids are looked up before anything is created, as legacy CLI checks
        // existence before it deploys, so a missing id fails without leaving a VNet or NSG behind.
        (string? existingNetworkSecurityGroupId, string? networkSecurityGroupNameToCreate) = await NewOrExistingResourceResolver.ResolveAsync(
            NetworkSecurityGroup, DefaultNetworkSecurityGroupName, SubscriptionId, ResourceGroupName,
            NewOrExistingResourceResolver.NetworkSecurityGroup, FindNetworkSecurityGroupIdAsync);
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

        Task<HttpResponseMessage> vnetResponseTask = vnetProcessor.SendAsync();
        Task<string?> networkSecurityGroupIdTask = networkSecurityGroupNameToCreate is null
            ? Task.FromResult(existingNetworkSecurityGroupId)
            : CreateNetworkSecurityGroupAsync(networkSecurityGroupNameToCreate);
        Task<string?> publicIpAddressIdTask = publicIpAddressNameToCreate is null
            ? Task.FromResult(existingPublicIpAddressId)
            : CreatePublicIpAddressAsync(publicIpAddressNameToCreate);
        using HttpResponseMessage vnetResponse = await vnetResponseTask;
        string? networkSecurityGroupId = await networkSecurityGroupIdTask;
        string? publicIpAddressId = await publicIpAddressIdTask;
        VirtualNetwork vnet = (await vnetResponse.Content.ReadFromJsonAsync<VirtualNetwork>(JsonOptions))!;

        NewNetworkInterfaceProcessor nicProcessor = CreateExtendedProcessor<NewNetworkInterfaceProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = Name;
            p.Location = Location;
            p.ResourceGroupName = ResourceGroupName;
            p.SubnetIds = vnet.Properties.Subnets.Select(subnet => subnet.Id).ToList();
            p.NetworkSecurityGroupId = networkSecurityGroupId ?? string.Empty;
            p.AcceleratedNetworking = AcceleratedNetworking;
            p.PublicIpAddressId = publicIpAddressId ?? string.Empty;
        });
        return await nicProcessor.SendAsync();
    }

    private Task<string?> FindNetworkSecurityGroupIdAsync(string subscriptionId, string resourceGroupName, string name)
        => FindIdAsync(NetworkResourceUri(subscriptionId, resourceGroupName, "networkSecurityGroups", name));

    private Task<string?> FindPublicIpAddressIdAsync(string subscriptionId, string resourceGroupName, string name)
        => FindIdAsync(NetworkResourceUri(subscriptionId, resourceGroupName, "publicIPAddresses", name));

    private static string NetworkResourceUri(string subscriptionId, string resourceGroupName, string resourceType, string name)
        => $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/{resourceType}/{name}?api-version=2025-05-01";

    /// <summary>
    /// Reads the network resource at <paramref name="uri"/> and returns its id, or null when ARM
    /// answers 404. Any ARM resource's body carries its id, so it reads as a reference.
    /// </summary>
    private async Task<string?> FindIdAsync(string uri)
    {
        using HttpResponseMessage response = await GetAllowingNotFoundAsync(uri);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        ResourceReference resource = (await response.Content.ReadFromJsonAsync<ResourceReference>(JsonOptions))!;
        return resource.Id;
    }

    /// <summary>
    /// A NotFound (404) safe GET, returning the response for the caller to read or dispose.
    /// </summary>
    private async Task<HttpResponseMessage> GetAllowingNotFoundAsync(string uri)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return response;
        }

        try
        {
            await response.EnsureSuccessAsync(Publisher);
        }
        catch
        {
            response.Dispose();
            throw;
        }
        return response;
    }

    private async Task<string?> CreateNetworkSecurityGroupAsync(string name)
    {
        SecurityRule defaultRule = NetworkSecurityGroupDefaultRule.For(OsType);
        using HttpResponseMessage response = await CreateExtendedProcessor<NewNetworkSecurityGroupProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = name;
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
        }).SendAsync();

        NetworkSecurityGroup networkSecurityGroup = (await response.Content.ReadFromJsonAsync<NetworkSecurityGroup>(JsonOptions))!;
        return networkSecurityGroup.Id;
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
