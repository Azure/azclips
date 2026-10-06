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

    public string? PublicIpAddress { get; set; }

    /// <summary>The public IP created when <see cref="PublicIpAddress"/> is null. Empty means none.</summary>
    public string DefaultPublicIpAddressName { get; set; } = string.Empty;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        // Explicit names and ids are looked up before anything is created, as legacy CLI checks
        // existence before it deploys, so a missing id fails without leaving a VNet or NSG behind.
        (string? existingPublicIpAddressId, string? publicIpAddressNameToCreate) = await ClassifyPublicIpAddressAsync();

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

    /// <summary>
    /// Determines the id of an existing public IP to attach, or the
    /// name of one to create, or neither when no public IP is wanted.
    /// </summary>
    private async Task<(string? ExistingId, string? NameToCreate)> ClassifyPublicIpAddressAsync()
    {
        string? value = PublicIpAddress;

        if (value is null)
        {
            if (string.IsNullOrWhiteSpace(DefaultPublicIpAddressName))
            {
                return (null, null);
            }
            return (null, DefaultPublicIpAddressName);
        }

        if (string.IsNullOrEmpty(value))
        {
            return (null, null);
        }

        if (value.Contains('/'))
        {
            var parsed = VirtualMachineIdResolver.ParseResourceId(
                value, "Microsoft.Network", "publicIPAddresses", "Public IP address", "publicIpAddressName");
            string existingId = await FindPublicIpAddressIdAsync(parsed.SubscriptionId, parsed.ResourceGroupName, parsed.Name)
                ?? throw new InvalidOperationException($"Public IP address '{value}' was not found.");
            return (existingId, null);
        }

        string? existingByName = await FindPublicIpAddressIdAsync(SubscriptionId, ResourceGroupName, value);
        if (existingByName is not null)
        {
            return (existingByName, null);
        }
        return (null, value);
    }

    private async Task<string?> FindPublicIpAddressIdAsync(string subscriptionId, string resourceGroupName, string name)
    {
        using HttpResponseMessage response = await GetPublicIpAddressAllowingNotFoundAsync(subscriptionId, resourceGroupName, name);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        PublicIPAddress publicIp = (await response.Content.ReadFromJsonAsync<PublicIPAddress>(JsonOptions))!;
        return publicIp.Id;
    }

    /// <summary>
    /// A NotFound (404) safe GET for a public IP address, returning the response for the caller to read or dispose.
    /// </summary>
    private async Task<HttpResponseMessage> GetPublicIpAddressAllowingNotFoundAsync(string subscriptionId, string resourceGroupName, string name)
    {
        using HttpRequestMessage request = new(HttpMethod.Get,
            $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/publicIPAddresses/{name}?api-version=2025-05-01");
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
