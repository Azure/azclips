// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class SubnetScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-subnet-rg";
    private const string VirtualNetworkName = "scenario-vnet";
    private const string SubnetName = "scenario-subnet";

    [Fact]
    public void CreateShowListDelete_ReplaysThroughTheCliSurface()
    {
        var record = RecordFromCli();

        using var cli = new ScenarioHost(
            ClientType.CLI,
            new RecordPlaybackInterceptor(RecorderMode.Playback, record),
            extensions: [ExtensionUnderTest.Network]);

        AssertSubnet(Assert.Single(cli.RunCli(CreateArgs())));
        AssertSubnet(Assert.Single(cli.RunCli(ShowArgs())));
        var listed = Assert.IsType<List<Subnet>>(Assert.Single(cli.RunCli(ListArgs())));
        Assert.Collection(
            listed,
            subnet => AssertSubnet(subnet),
            subnet => AssertSubnet(subnet, "scenario-subnet-page-2"));
        cli.RunCli(DeleteArgs());
    }

    [Fact]
    public void Create_UsesLegacyShape_AndDeletePollsOnlyTheOperationUri()
    {
        var record = RecordFromCli();
        var create = record.Entries.First(entry => entry.Request.Method == "PUT");

        Assert.Contains($"/virtualNetworks/{VirtualNetworkName}/subnets/{SubnetName}", create.Request.Uri);
        Assert.Contains("api-version=2025-07-01", create.Request.Uri);
        Assert.Contains("\"addressPrefixes\":[\"10.20.1.0/24\"]", create.Request.Body);
        Assert.DoesNotContain("\"addressPrefix\":", create.Request.Body);
        Assert.Contains("\"service\":\"Microsoft.Storage\"", create.Request.Body);
        Assert.Contains("\"serviceName\":\"Microsoft.Web/serverFarms\"", create.Request.Body);
        Assert.Contains("\"defaultOutboundAccess\":false", create.Request.Body);
        Assert.DoesNotContain("networkSecurityGroup", create.Request.Body);
        Assert.Contains(record.Entries, entry => entry.Request.Uri.Contains("/operations/subnet-create"));

        var deleteIndex = record.Entries.FindIndex(entry => entry.Request.Method == "DELETE");
        var afterDelete = record.Entries.Skip(deleteIndex + 1).ToList();
        Assert.NotEmpty(afterDelete);
        Assert.All(afterDelete, entry => Assert.Contains("/operations/subnet-delete", entry.Request.Uri));
    }

    private static SessionRecord RecordFromCli()
    {
        var record = new SessionRecord();
        using var host = new ScenarioHost(
            ClientType.CLI,
            new RecordPlaybackInterceptor(RecorderMode.Record, record),
            new FakeSubnetArmBackend(SubscriptionId, ResourceGroupName, VirtualNetworkName),
            extensions: [ExtensionUnderTest.Network]);

        AssertSubnet(Assert.Single(host.RunCli(CreateArgs())));
        AssertSubnet(Assert.Single(host.RunCli(ShowArgs())));
        Assert.Equal(2, Assert.IsType<List<Subnet>>(Assert.Single(host.RunCli(ListArgs()))).Count);
        host.RunCli(DeleteArgs());
        return record;
    }

    private static string[] CreateArgs() =>
    [
        "network", "vnet", "subnet", "create",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--vnet-name", VirtualNetworkName,
        "--name", SubnetName,
        "--address-prefixes", "10.20.1.0/24",
        "--service-endpoints", "Microsoft.Storage",
        "--delegations", "Microsoft.Web/serverFarms",
        "--default-outbound-access", "false"
    ];

    private static string[] ShowArgs() =>
    [
        "network", "vnet", "subnet", "show",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--vnet-name", VirtualNetworkName,
        "--name", SubnetName
    ];

    private static string[] ListArgs() =>
    [
        "network", "vnet", "subnet", "list",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--vnet-name", VirtualNetworkName
    ];

    private static string[] DeleteArgs() =>
    [
        "network", "vnet", "subnet", "delete",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--vnet-name", VirtualNetworkName,
        "--name", SubnetName
    ];

    private static void AssertSubnet(object output, string expectedName = SubnetName)
    {
        var subnet = Assert.IsType<Subnet>(output);
        Assert.Equal(expectedName, subnet.Name);
        Assert.Equal("10.20.1.0/24", Assert.Single(subnet.AddressPrefixes ?? []));
        Assert.Equal("Microsoft.Storage", Assert.Single(subnet.ServiceEndpoints ?? []).Service);
        Assert.Equal("Microsoft.Web/serverFarms", Assert.Single(subnet.Delegations ?? []).ServiceName);
        Assert.False(subnet.DefaultOutboundAccess);
    }
}
