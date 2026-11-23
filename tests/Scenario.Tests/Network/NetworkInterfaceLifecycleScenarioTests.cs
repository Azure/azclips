using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Recording;
using static Scenario.Tests.NetworkInterfaceScenario;

namespace Scenario.Tests;

/// <summary>
/// Network interface lifecycle: create with defaults, show, list in the group and in the subscription,
/// delete. Test spec: <c>network-network-interfaces-tests</c>.
/// </summary>
[Collection("Scenario Tests")]
public class NetworkInterfaceLifecycleScenarioTests
{
    private const string Location = "eastus2";
    private const string SubnetPrefix = "10.0.1.0/24";

    // T1, test spec scenario "T1 Create with defaults, read, list and delete a network interface"
    // (requirement "List scope by resource group presence").
    [Fact]
    public void NetworkInterfaceLifecycleCreateShowListDelete()
    {
        const string resourceGroup = "scenario-nic-lifecycle-rg";

        RunCase(
            typeof(NetworkInterfaceLifecycleScenarioTests),
            nameof(NetworkInterfaceLifecycleCreateShowListDelete),
            resourceGroup,
            cli: run =>
            {
                CliGroupCreate(run, Location);
                CliVnetCreate(run, Location);
                CliSubnetCreate(run, SubnetPrefix);

                int sent = run.Traffic.Sent.Count;
                var created = Single(Cli(run, "nic", "create", "-g", run.ResourceGroup, "-n", NicName, "-l", Location,
                    "--subnet", SubnetName, "--vnet-name", VnetName));
                AssertCreated(created);
                Assert.EndsWith($"/virtualNetworks/{VnetName}/subnets/{SubnetName}", created.IpConfigurations![0].SubnetId, StringComparison.OrdinalIgnoreCase);
                Assert.EndsWith(NetworkResourceIdSuffix(run, "virtualNetworks", $"{VnetName}/subnets/{SubnetName}"),
                    PutBody(run, sent)["properties"]!["ipConfigurations"]![0]!["properties"]!["subnet"]!["id"]!.GetValue<string>(),
                    StringComparison.OrdinalIgnoreCase);

                AssertShown(CliShow(run));

                sent = run.Traffic.Sent.Count;
                var inGroup = Items(Cli(run, "nic", "list", "-g", run.ResourceGroup));
                AssertGroupList(run, FirstGetUri(run, sent).AbsolutePath, inGroup);
                Assert.All(inGroup, nic => Assert.Equal("Microsoft.Network/networkInterfaces", nic.Type));

                sent = run.Traffic.Sent.Count;
                var all = Items(Cli(run, "nic", "list"));
                AssertSubscriptionList(run, FirstGetUri(run, sent).AbsolutePath, all);
                Assert.All(all, nic => Assert.Contains("/networkInterfaces/", nic.Id, StringComparison.OrdinalIgnoreCase));

                sent = run.Traffic.Sent.Count;
                Cli(run, "nic", "delete", "-g", run.ResourceGroup, "-n", NicName);
                AssertDeleteSent(run, sent);

                Assert.Empty(Items(Cli(run, "nic", "list", "-g", run.ResourceGroup)));
                AssertNotFound(() => CliShow(run));

                CliGroupDelete(run);
            },
            cliSubnet: run => CliSubnetCreate(run, SubnetPrefix),
            powerShell: (run, subnetId) =>
            {
                PsGroupCreate(run, Location);
                PsVnetCreate(run, Location);

                var created = Single(Ps(run, "New-AzNetworkInterface", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = NicName,
                    ["Location"] = Location,
                    ["SubnetId"] = subnetId,
                }));
                AssertCreated(created);
                Assert.Equal(subnetId, created.IpConfigurations![0].SubnetId, ignoreCase: true);

                AssertShown(PsShow(run));

                int sent = run.Traffic.Sent.Count;
                var inGroup = Items(Ps(run, "Get-AzNetworkInterface_List", new() { ["ResourceGroupName"] = run.ResourceGroup }));
                AssertGroupList(run, FirstGetUri(run, sent).AbsolutePath, inGroup);
                Assert.Equal("Succeeded", Assert.Single(inGroup, nic => nic.Name == NicName).ProvisioningState);

                sent = run.Traffic.Sent.Count;
                AssertSubscriptionList(run, FirstGetUri(run, sent).AbsolutePath, Items(Ps(run, "Get-AzNetworkInterface_List", new())));

                sent = run.Traffic.Sent.Count;
                Ps(run, "Remove-AzNetworkInterface", new() { ["ResourceGroupName"] = run.ResourceGroup, ["Name"] = NicName });
                AssertDeleteSent(run, sent);

                Assert.Empty(Items(Ps(run, "Get-AzNetworkInterface_List", new() { ["ResourceGroupName"] = run.ResourceGroup })));
                AssertNotFound(() => PsShow(run));

                PsGroupDelete(run);
            },
            // The subscription-wide list returns every network interface in the recording subscription;
            // keep only this case's group so nobody else's interfaces reach the committed recording.
            sanitizers: RecordingSanitizers.Default.With([new ResourceGroupListSanitizer(resourceGroup)]));
    }

    private static void AssertCreated(NetworkInterface nic)
    {
        Assert.Equal(NicName, nic.Name);
        Assert.Equal("Succeeded", nic.ProvisioningState);
        var ipConfiguration = Assert.Single(nic.IpConfigurations ?? []);
        Assert.Equal("ipconfig1", ipConfiguration.Name);
        Assert.Equal("Dynamic", ipConfiguration.PrivateIPAllocationMethod);
        Assert.Null(ipConfiguration.PublicIPAddress);
    }

    private static void AssertShown(NetworkInterface nic)
    {
        Assert.Equal(NicName, nic.Name);
        Assert.Equal(Location, nic.Location);
        Assert.Equal("Microsoft.Network/networkInterfaces", nic.Type);
        Assert.Equal("Dynamic", nic.IpConfigurations![0].PrivateIPAllocationMethod);
    }

    private static void AssertGroupList(NetworkInterfaceRun run, string path, List<NetworkInterface> items)
    {
        Assert.Contains($"/resourceGroups/{run.ResourceGroup}{NetworkInterfacesPath}", path, StringComparison.OrdinalIgnoreCase);
        Assert.Single(items, nic => nic.Name == NicName);
    }

    // The list without a group is ListAll; the subscription is the developer's while recording,
    // so its exact value is asserted only in playback.
    private static void AssertSubscriptionList(NetworkInterfaceRun run, string path, List<NetworkInterface> all)
    {
        Assert.DoesNotContain("/resourceGroups/", path, StringComparison.OrdinalIgnoreCase);
        if (run.SubscriptionId is not null)
        {
            Assert.Equal($"/subscriptions/{run.SubscriptionId}{NetworkInterfacesPath}", path, ignoreCase: true);
        }
        else
        {
            Assert.StartsWith("/subscriptions/", path, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(NetworkInterfacesPath, path, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(all, nic => nic.Name == NicName);
    }
}
