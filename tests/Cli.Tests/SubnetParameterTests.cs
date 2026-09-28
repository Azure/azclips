// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Microsoft.Azure.Azclips.Extensions.Network.Processors;
using NetworkIndex = Microsoft.Azure.Azclips.Extensions.Network.ExtensionIndex;
using Xunit;

namespace Cli.Tests;

public class SubnetParameterTests
{
    private const string SubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private static void Init(IProcessor processor, FakeHttpPipeline pipeline, FakeOutputHandler output)
    {
        pipeline.PollingInterval = 0;
        var profile = new Profile { TenantId = "test-tenant", SubscriptionId = SubscriptionId };
        processor.Init(new ProcessorContext(pipeline, new FakeEventPublisher(), new FakeAuthHandler(profile), output, Profile: profile));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Create_MapsLegacyArgumentsWithoutProcessorHelpers()
    {
        string? requestBody = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requestBody ??= request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""{ "name": "web", "properties": { "provisioningState": "Succeeded" } }""");
        });
        var processor = new NewSubnetProcessor_Customization
        {
            SubscriptionId = SubscriptionId,
            ResourceGroupName = "my-rg",
            VirtualNetworkName = "my-vnet",
            Name = "web",
            AddressPrefixes = ["10.0.1.0/24"],
            NetworkSecurityGroup = "web-nsg",
            RouteTable = "web-routes",
            ServiceEndpoints = ["Microsoft.Storage"],
            DelegatedServices = ["Microsoft.Web/serverFarms"],
            DefaultOutboundAccessValue = false
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        var body = JsonDocument.Parse(requestBody!).RootElement;
        var properties = body.GetProperty("properties");
        Assert.Equal("10.0.1.0/24", properties.GetProperty("addressPrefix").GetString());
        Assert.EndsWith("/networkSecurityGroups/web-nsg", properties.GetProperty("networkSecurityGroup").GetProperty("id").GetString());
        Assert.EndsWith("/routeTables/web-routes", properties.GetProperty("routeTable").GetProperty("id").GetString());
        Assert.Equal("Microsoft.Storage", properties.GetProperty("serviceEndpoints")[0].GetProperty("service").GetString());
        Assert.Equal("Microsoft.Web/serverFarms", properties.GetProperty("delegations")[0].GetProperty("properties").GetProperty("serviceName").GetString());
        Assert.False(properties.GetProperty("defaultOutboundAccess").GetBoolean());
    }

    [Fact]
    public async Task Create_OmittedDefaultOutboundAccess_RemainsNull()
    {
        string? requestBody = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requestBody ??= request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""{ "name": "web", "properties": {} }""");
        });
        var processor = new NewSubnetProcessor_Customization
        {
            SubscriptionId = SubscriptionId,
            ResourceGroupName = "my-rg",
            VirtualNetworkName = "my-vnet",
            Name = "web",
            AddressPrefixes = ["10.0.1.0/24"]
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.False(JsonDocument.Parse(requestBody!).RootElement.GetProperty("properties").TryGetProperty("defaultOutboundAccess", out _));
    }

    [Fact]
    public async Task Create_MapsIpamObjectsClearsAddressPrefixesAndMapsLegacyPolicyFlags()
    {
        string? requestBody = null;
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requestBody ??= request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""{ "name": "web", "properties": {} }""");
        });
        var processor = new NewSubnetProcessor_Customization
        {
            SubscriptionId = SubscriptionId,
            ResourceGroupName = "my-rg",
            VirtualNetworkName = "my-vnet",
            Name = "web",
            AddressPrefixes = ["10.0.1.0/24"],
            IpamPoolPrefixAllocationSpecs =
            [
                ["id=/subscriptions/s/resourceGroups/rg/providers/Microsoft.Network/networkManagers/nm/ipamPools/pool", "number-of-ip-addresses=256"]
            ],
            DisablePrivateEndpointNetworkPolicies = true,
            DisablePrivateLinkServiceNetworkPolicies = false
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        var properties = JsonDocument.Parse(requestBody!).RootElement.GetProperty("properties");
        var allocation = properties.GetProperty("ipamPoolPrefixAllocations")[0];
        Assert.EndsWith("/ipamPools/pool", allocation.GetProperty("pool").GetProperty("id").GetString());
        Assert.Equal("256", allocation.GetProperty("numberOfIpAddresses").GetString());
        Assert.Equal("Disabled", properties.GetProperty("privateEndpointNetworkPolicies").GetString());
        Assert.Equal("Enabled", properties.GetProperty("privateLinkServiceNetworkPolicies").GetString());
        Assert.Equal(string.Empty, properties.GetProperty("addressPrefix").GetString());
        Assert.Empty(properties.GetProperty("addressPrefixes").EnumerateArray());
    }

    [Fact]
    public async Task List_UnwrapsSubnetEnvelope()
    {
        var output = new FakeOutputHandler();
        var processor = new ListSubnetProcessor
        {
            SubscriptionId = SubscriptionId,
            ResourceGroupName = "my-rg",
            VirtualNetworkName = "my-vnet"
        };
        Init(processor, new FakeHttpPipeline((_, _) => Json("""{ "value": [{ "name": "web", "properties": {} }] }""")), output);

        await processor.ProcessAsync();

        var subnets = Assert.IsType<List<Subnet>>(Assert.Single(output.WrittenValues));
        Assert.Equal("web", Assert.Single(subnets).Name);
    }

    [Fact]
    public async Task NewSubnetConfig_BuildsAnOfflineSubnetObject()
    {
        var output = new FakeOutputHandler();
        var processor = new NewSubnetConfigProcessor_Customization
        {
            Name = "web",
            AddressPrefixes = ["10.0.1.0/24"],
            ServiceEndpoints = ["Microsoft.Storage"]
        };
        Init(processor, new FakeHttpPipeline((_, _) => throw new InvalidOperationException("Subnet config creation must not call ARM.")), output);

        await processor.ProcessAsync();

        var subnet = Assert.IsType<Subnet>(Assert.Single(output.WrittenValues));
        Assert.Equal("web", subnet.Name);
        Assert.Equal("10.0.1.0/24", subnet.Properties.AddressPrefix);
        Assert.Equal("Microsoft.Storage", Assert.Single(subnet.Properties.ServiceEndpoints).Service);
    }

    [Theory]
    [InlineData("network vnet subnet create", "g", "resource-group")]
    [InlineData("network vnet subnet create", "n", "name")]
    [InlineData("network vnet subnet create", "nsg", "network-security-group")]
    [InlineData("network vnet subnet create", "delegated-services", "delegations")]
    [InlineData("network vnet subnet create", "ipam-pool-prefix-allocations", "ipam-allocations")]
    [InlineData("network vnet subnet show", "g", "resource-group")]
    [InlineData("network vnet subnet delete", "n", "name")]
    public void CliCommands_MapLegacyAliases(string command, string alias, string canonical)
    {
        var metadata = new NetworkIndex().GetCommandMetadata(command, ClientType.CLI);

        Assert.Equal(canonical, metadata.ParameterAliases[alias]);
        Assert.Contains(alias, metadata.Parameters[canonical].Aliases);
    }
}
