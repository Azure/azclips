using System.Web;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using static Scenario.Tests.NetworkInterfaceScenario;

namespace Scenario.Tests;

/// <summary>
/// Network interface create with a static private IP, a public IP address, IP forwarding, accelerated
/// networking, DNS settings and tags; show with and without expand. Test spec: <c>network-network-interfaces-tests</c>.
/// </summary>
[Collection("Scenario Tests")]
public class NetworkInterfaceCreateOptionsScenarioTests
{
    private const string Location = "eastus2";
    private const string SubnetPrefix = "10.0.1.0/24";
    private const string PipName = "nicpip";
    private const string PrivateIp = "10.0.1.5";

    // T2, test spec scenario "T2 Create with static private IP, public IP by name, and optional settings;
    // show with expand" (requirement "Single IP configuration from flat create inputs").
    [Fact]
    public void CreateWithStaticPrivateIpPublicIpAndOptionalSettingsThenShowWithExpand()
    {
        RunCase(
            typeof(NetworkInterfaceCreateOptionsScenarioTests),
            nameof(CreateWithStaticPrivateIpPublicIpAndOptionalSettingsThenShowWithExpand),
            "scenario-nic-options-rg",
            cli: run =>
            {
                CliGroupCreate(run, Location);
                CliVnetCreate(run, Location);
                var subnet = CliSubnetCreate(run, SubnetPrefix);
                Cli(run, "public-ip", "create", "-g", run.ResourceGroup, "-n", PipName, "-l", Location, "--sku", "Standard", "--allocation-method", "Static");

                int sent = run.Traffic.Sent.Count;
                var created = Single(Cli(run, "nic", "create", "-g", run.ResourceGroup, "-n", NicName, "-l", Location,
                    "--subnet", subnet.Id!, "--private-ip-address", PrivateIp, "--public-ip-address", PipName,
                    "--ip-forwarding", "true", "--accelerated-networking", "true", "--internal-dns-name", "test",
                    "--dns-servers", "100.1.2.3", "--tags", "foo=doo"));
                AssertCreated(created);
                Assert.EndsWith($"/publicIPAddresses/{PipName}", created.IpConfigurations![0].PublicIpAddressId, StringComparison.OrdinalIgnoreCase);
                Assert.EndsWith(NetworkResourceIdSuffix(run, "publicIPAddresses", PipName),
                    PutBody(run, sent)["properties"]!["ipConfigurations"]![0]!["properties"]!["publicIPAddress"]!["id"]!.GetValue<string>(),
                    StringComparison.OrdinalIgnoreCase);

                var shown = CliShow(run);
                AssertShown(shown);
                Assert.Equal("Static", shown.IpConfigurations![0].PrivateIPAllocationMethod);

                sent = run.Traffic.Sent.Count;
                var expanded = CliShow(run, "--expand", "ipConfigurations/publicIPAddress");
                AssertExpanded(run, sent, expanded, "ipConfigurations/publicIPAddress");

                CliGroupDelete(run);
            },
            cliSubnet: run => CliSubnetCreate(run, SubnetPrefix),
            powerShell: (run, subnetId) =>
            {
                PsGroupCreate(run, Location);
                PsVnetCreate(run, Location);
                var pip = Assert.IsType<PublicIPAddress>(Assert.Single(Ps(run, "New-AzPublicIpAddress", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = PipName,
                    ["Location"] = Location,
                    ["Sku"] = "Standard",
                    ["AllocationMethod"] = "Static",
                })));

                int sent = run.Traffic.Sent.Count;
                var created = Single(Ps(run, "New-AzNetworkInterface", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = NicName,
                    ["Location"] = Location,
                    ["SubnetId"] = subnetId,
                    ["PublicIpAddressId"] = pip.Id!,
                    ["PrivateIpAddress"] = PrivateIp,
                    ["IpConfigurationName"] = "createipconf",
                    ["EnableIPForwarding"] = true,
                    ["EnableAcceleratedNetworking"] = true,
                    ["InternalDnsNameLabel"] = "test",
                    ["DnsServer"] = new[] { "100.1.2.3" },
                    ["Tag"] = new Dictionary<string, string> { ["foo"] = "doo" },
                }));
                // The playback response is the CLI recording's (ipconfig1), so the PowerShell-only
                // IP configuration name is checked on the request this cmdlet sent.
                Assert.Equal("createipconf", PutBody(run, sent)["properties"]!["ipConfigurations"]![0]!["name"]!.GetValue<string>());
                AssertCreated(created);
                Assert.Equal(pip.Id, created.IpConfigurations![0].PublicIpAddressId, ignoreCase: true);

                AssertShown(PsShow(run));

                sent = run.Traffic.Sent.Count;
                AssertExpanded(run, sent, PsShow(run, "IpConfigurations/PublicIPAddress"), "IpConfigurations/PublicIPAddress");

                PsGroupDelete(run);
            });
    }

    private static void AssertCreated(NetworkInterface nic)
    {
        Assert.Equal("Succeeded", nic.ProvisioningState);
        var ipConfiguration = nic.IpConfigurations![0];
        Assert.Equal("Static", ipConfiguration.PrivateIPAllocationMethod);
        Assert.Equal(PrivateIp, ipConfiguration.PrivateIPAddress);
        Assert.True(nic.EnableIPForwarding);
        Assert.True(nic.EnableAcceleratedNetworking);
        Assert.Equal("test", nic.DnsSettingsInternalDnsNameLabel);
        Assert.Equal(["100.1.2.3"], nic.DnsSettingsDnsServers ?? []);
        Assert.Equal("doo", Assert.Contains("foo", nic.Tags ?? new Dictionary<string, string>()));
    }

    private static void AssertShown(NetworkInterface nic)
    {
        Assert.Equal(PrivateIp, nic.IpConfigurations![0].PrivateIPAddress);
        Assert.Equal("test", nic.DnsSettingsInternalDnsNameLabel);
        Assert.Null(nic.IpConfigurations[0].PublicIpAddressName);
    }

    private static void AssertExpanded(NetworkInterfaceRun run, int sentBefore, NetworkInterface nic, string expand)
    {
        Assert.Equal(expand, HttpUtility.ParseQueryString(FirstGetUri(run, sentBefore).Query)["$expand"]);
        Assert.Equal(PipName, nic.IpConfigurations![0].PublicIpAddressName);
    }
}
