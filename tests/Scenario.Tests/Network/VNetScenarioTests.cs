using Core.Abstractions;
using Tests.Recording;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises the virtual network lifecycle through the real dispatcher/binder and ARM request
/// pipeline on both client surfaces. Record mode runs the CLI surface against live Azure and
/// rewrites the recording; playback replays the committed recording without credentials.
/// <para>
/// The create covers the parameters that legacy <c>az network vnet create</c> and
/// <c>New-AzVirtualNetwork</c> callers rely on: a compound subnet declaration, DNS servers,
/// tags, and a flow timeout.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class VNetScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-vnet-rg";
    private const string VNetName = "scenario-vnet";
    private const string Location = "westus2";

    [Fact]
    public void CreateShowListDeleteVirtualNetwork()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(VNetScenarioTests), nameof(CreateShowListDeleteVirtualNetwork));

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                useLiveAuth: true,
                extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]);

            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                host.RunCli(GroupArgs(subscriptionId, groupName));
                AssertCreated(host.RunCli(CreateArgs(subscriptionId, groupName)));
                AssertShown(host.RunCli(ShowArgs(subscriptionId, groupName)));
                AssertListed(host.RunCli(ListArgs(subscriptionId, groupName)));
                host.RunCli(DeleteArgs(subscriptionId, groupName));
                recording.Save();
            }
            finally
            {
                // After the save, so the delete never lands in the recording.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
            }
            return;
        }

        using (var cli = new ScenarioHost(
            ClientType.CLI,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]))
        {
            cli.RunCli(GroupArgs(SubscriptionId));
            AssertCreated(cli.RunCli(CreateArgs(SubscriptionId)));
            AssertShown(cli.RunCli(ShowArgs(SubscriptionId)));
            AssertListed(cli.RunCli(ListArgs(SubscriptionId)));
            cli.RunCli(DeleteArgs(SubscriptionId));
        }

        using (var ps = new ScenarioHost(
            ClientType.PowerShell,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(), extension: "Resources");
            AssertCreated(ps.RunPowerShell("New-AzVirtualNetwork", PsCreateParameters(), extension: "Network"));
            AssertShown(ps.RunPowerShell("Get-AzVirtualNetwork_Get", PsShowParameters(), extension: "Network"));
            AssertListed(ps.RunPowerShell("Get-AzVirtualNetwork_List", PsListParameters(), extension: "Network"));
            ps.RunPowerShell("Remove-AzVirtualNetwork", PsShowParameters(), extension: "Network");
        }
    }

    private static string[] GroupArgs(string subscriptionId, string? groupName = null) =>
    [
        "group", "create",
        "--subscription-id", subscriptionId,
        "--name", groupName ?? ResourceGroupName,
        "--location", Location,
    ];

    private static string[] CreateArgs(string subscriptionId, string? groupName = null) =>
    [
        "network", "vnet", "create",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName ?? ResourceGroupName,
        "--name", VNetName,
        "--location", Location,
        "--address-prefixes", "10.0.0.0/16",
        "--dns-servers", "10.0.0.4", "10.0.0.5",
        "--tags", "env=scenario",
        "--flowtimeout", "10",
        "--subnets", "name=web", "address-prefixes=10.0.1.0/24", "default-outbound-access=false",
        "--subnets", "name=data", "address-prefixes=10.0.2.0/24", "service-endpoints=Microsoft.Storage",
    ];

    private static string[] ShowArgs(string subscriptionId, string? groupName = null) =>
    [
        "network", "vnet", "show",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName ?? ResourceGroupName,
        "--name", VNetName,
    ];

    private static string[] ListArgs(string subscriptionId, string? groupName = null) =>
    [
        "network", "vnet", "list",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName ?? ResourceGroupName,
    ];

    private static string[] DeleteArgs(string subscriptionId, string? groupName = null) =>
    [
        "network", "vnet", "delete",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName ?? ResourceGroupName,
        "--name", VNetName,
    ];

    private static Dictionary<string, object> PsGroupParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["Name"] = ResourceGroupName,
        ["Location"] = Location,
    };

    private static Dictionary<string, object> PsCreateParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = VNetName,
        ["Location"] = Location,
        ["AddressPrefix"] = new List<string> { "10.0.0.0/16" },
        ["DnsServer"] = new List<string> { "10.0.0.4", "10.0.0.5" },
        ["Tag"] = new List<string> { "env=scenario" },
        ["FlowTimeout"] = 10,
        ["Subnet"] = new List<List<string>>
        {
            new() { "Name=web", "AddressPrefixes=10.0.1.0/24", "DefaultOutboundAccess=false" },
            new() { "Name=data", "AddressPrefixes=10.0.2.0/24", "ServiceEndpoint=Microsoft.Storage" },
        },
    };

    private static Dictionary<string, object> PsShowParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = VNetName,
    };

    private static Dictionary<string, object> PsListParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
    };

    private static void AssertCreated(IReadOnlyList<object> outputs) => AssertVNet(Assert.Single(outputs));

    private static void AssertShown(IReadOnlyList<object> outputs) => AssertVNet(Assert.Single(outputs));

    private static void AssertListed(IReadOnlyList<object> outputs)
    {
        var vnets = Assert.IsType<List<VirtualNetwork>>(Assert.Single(outputs));
        AssertVNet(Assert.Single(vnets));
    }

    private static void AssertVNet(object output)
    {
        var vnet = Assert.IsType<VirtualNetwork>(output);
        Assert.Equal(VNetName, vnet.Name);
        Assert.Equal(Location, vnet.Location);
        Assert.Equal("Succeeded", vnet.Properties.ProvisioningState);
        Assert.Equal("scenario", vnet.Tags?["env"]);
        Assert.Equal("10.0.0.0/16", Assert.Single(vnet.Properties.AddressSpace.AddressPrefixes));
        Assert.Equal(10, vnet.Properties.FlowTimeoutInMinutes);
        Assert.Equal(["10.0.0.4", "10.0.0.5"], vnet.Properties.DhcpOptions?.DnsServers ?? []);

        Assert.Collection(
            vnet.Properties.Subnets,
            web =>
            {
                Assert.Equal("web", web.Name);
                Assert.Equal("10.0.1.0/24", web.Properties.AddressPrefix);
                Assert.False(web.Properties.DefaultOutboundAccess);
            },
            data =>
            {
                Assert.Equal("data", data.Name);
                Assert.Equal("10.0.2.0/24", data.Properties.AddressPrefix);
                Assert.Equal("Microsoft.Storage", Assert.Single(data.Properties.ServiceEndpoints).Service);
            });
    }
}
