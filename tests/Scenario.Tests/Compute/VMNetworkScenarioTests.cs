using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// vm create fills in each omitted virtual network prefix on its own, as legacy az vm create does: the
/// VNet range defaults to 10.0.0.0/16 and the subnet range to 10.0.0.0/24. Record mode runs against live
/// Azure and rewrites the recording; playback replays it without credentials. A subnet range outside the
/// VNet range is not checked client-side, so ARM's rejection surfaces as the error.
/// </summary>
[Collection("Scenario Tests")]
public class VMNetworkScenarioTests
{
    // Playback matches the placeholder the sanitisers wrote into the recording.
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string Location = "australiaeast";
    private const string Password = "PlaceholderP@ss1";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute, ExtensionUnderTest.Network];

    [Fact]
    public void AddressPrefixesOnly_DefaultsSubnetAddressPrefix()
    {
        RunScenario(nameof(AddressPrefixesOnly_DefaultsSubnetAddressPrefix), "scen-vmnet-a",
            (host, subscriptionId, groupName, vmName) =>
            {
                host.RunCli(VmCreateArgs(subscriptionId, groupName, vmName, "--address-prefixes", "10.0.0.0/16"));
                AssertVnetPrefixes(host, subscriptionId, groupName, vmName, "10.0.0.0/16", "10.0.0.0/24");
            });
    }

    [Fact]
    public void SubnetAddressPrefixesOnly_DefaultsAddressPrefixes()
    {
        RunScenario(nameof(SubnetAddressPrefixesOnly_DefaultsAddressPrefixes), "scen-vmnet-s",
            (host, subscriptionId, groupName, vmName) =>
            {
                host.RunCli(VmCreateArgs(subscriptionId, groupName, vmName, "--subnet-address-prefixes", "10.0.0.0/24"));
                AssertVnetPrefixes(host, subscriptionId, groupName, vmName, "10.0.0.0/16", "10.0.0.0/24");
            });
    }

    [Fact]
    public void SubnetOutsideAddressPrefixes_IsRejectedByArm()
    {
        RunScenario(nameof(SubnetOutsideAddressPrefixes_IsRejectedByArm), "scen-vmnet-x",
            (host, subscriptionId, groupName, vmName) =>
            {
                // The default subnet, 10.0.0.0/24, is not inside 192.168.0.0/16.
                Assert.IsType<HttpRequestException>(Assert.ThrowsAny<Exception>(() =>
                    host.RunCli(VmCreateArgs(subscriptionId, groupName, vmName, "--address-prefixes", "192.168.0.0/16"))));
            });
    }

    private static void RunScenario(string testName, string baseName, Action<ScenarioHost, string, string, string> scenario)
    {
        var recording = SessionRecordFiles.OpenSession(typeof(VMNetworkScenarioTests), testName);

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(baseName + "-rg", recording.Mode);
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: s_extensions);
            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                host.RunCli("group", "create", "--subscription-id", subscriptionId, "--name", groupName, "--location", Location);
                scenario(host, subscriptionId, groupName, baseName);
                recording.Save();
            }
            finally
            {
                // After the save, so the delete never lands in the recording.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
            }
            return;
        }

        using var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions);
        cli.RunCli("group", "create", "--subscription-id", SubscriptionId, "--name", baseName + "-rg", "--location", Location);
        scenario(cli, SubscriptionId, baseName + "-rg", baseName);
    }

    private static string[] VmCreateArgs(string subscriptionId, string groupName, string vmName, params string[] networkArgs) =>
    [
        "vm", "create",
        "--subscription-id", subscriptionId,
        "--name", vmName,
        "--location", Location,
        "--resource-group", groupName,
        "--size", "Standard_D2ds_v6",
        "--admin-username", "azadmin",
        "--admin-password", Password,
        "--virtual-network-name", $"{vmName}-vnet",
        .. networkArgs,
    ];

    private static void AssertVnetPrefixes(ScenarioHost host, string subscriptionId, string groupName, string vmName, string vnetPrefix, string subnetPrefix)
    {
        var vnet = Assert.IsType<VirtualNetwork>(Assert.Single(host.RunCli(
            "network", "vnet", "show",
            "--subscription-id", subscriptionId,
            "--name", $"{vmName}-vnet",
            "--resource-group", groupName)));
        Assert.Equal([vnetPrefix], vnet.AddressSpaceAddressPrefixes);
        Assert.Equal(subnetPrefix, Assert.Single(vnet.Subnets!).AddressPrefix);
    }
}
