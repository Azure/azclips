using Microsoft.Azure.Azclips.Extensions.Network.Models;
using static Scenario.Tests.NetworkInterfaceScenario;

namespace Scenario.Tests;

/// <summary>
/// Network interface create with network security group and public IP address references (CLI by name
/// and by ID), then re-create without IP forwarding. Test spec: <c>network-network-interfaces-tests</c>.
/// </summary>
[Collection("Scenario Tests")]
public class NetworkInterfaceReferencesScenarioTests
{
    private const string Location = "eastus2";
    private const string SubnetPrefix = "10.0.1.0/24";
    private const string NsgName = "nicnsg";
    private const string PipName = "nicpip";

    // T3, test spec scenario "T3 Create with network security group and public IP references, then re-create
    // without IP forwarding" (requirement "CLI name-or-ID expansion for create references").
    [Fact]
    public void CreateWithSecurityGroupAndPublicIpReferencesThenRecreateWithoutIpForwarding()
    {
        RunCase(
            typeof(NetworkInterfaceReferencesScenarioTests),
            nameof(CreateWithSecurityGroupAndPublicIpReferencesThenRecreateWithoutIpForwarding),
            "scenario-nic-refs-rg",
            cli: run =>
            {
                CliGroupCreate(run, Location);
                CliVnetCreate(run, Location);
                var subnet = CliSubnetCreate(run, SubnetPrefix);
                var nsg = Assert.IsType<NetworkSecurityGroup>(Assert.Single(Cli(run, "nsg", "create", "-g", run.ResourceGroup, "-n", NsgName, "-l", Location)));
                var pip = Assert.IsType<PublicIPAddress>(Assert.Single(Cli(run, "public-ip", "create", "-g", run.ResourceGroup, "-n", PipName,
                    "-l", Location, "--sku", "Standard", "--allocation-method", "Static")));

                int sent = run.Traffic.Sent.Count;
                var first = Single(Cli(run, "nic", "create", "-g", run.ResourceGroup, "-n", NicName, "-l", Location,
                    "--subnet", SubnetName, "--vnet-name", VnetName, "--network-security-group", NsgName, "--ip-forwarding", "true"));
                Assert.EndsWith($"/networkSecurityGroups/{NsgName}", first.NetworkSecurityGroupId, StringComparison.OrdinalIgnoreCase);
                Assert.EndsWith(NetworkResourceIdSuffix(run, "networkSecurityGroups", NsgName),
                    PutBody(run, sent)["properties"]!["networkSecurityGroup"]!["id"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
                Assert.True(first.EnableIPForwarding);
                Assert.Equal("Dynamic", first.IpConfigurations![0].PrivateIPAllocationMethod);

                sent = run.Traffic.Sent.Count;
                var second = Single(Cli(run, "nic", "create", "-g", run.ResourceGroup, "-n", NicName, "-l", Location,
                    "--subnet", subnet.Id!, "--network-security-group", nsg.Id!, "--public-ip-address", pip.Id!));
                var body = PutBody(run, sent)["properties"]!;
                Assert.Equal(nsg.Id, body["networkSecurityGroup"]!["id"]!.GetValue<string>());
                Assert.Equal(pip.Id, body["ipConfigurations"]![0]!["properties"]!["publicIPAddress"]!["id"]!.GetValue<string>());
                AssertRecreated(body, second);

                CliGroupDelete(run);
            },
            cliSubnet: run => CliSubnetCreate(run, SubnetPrefix),
            powerShell: (run, subnetId) =>
            {
                PsGroupCreate(run, Location);
                PsVnetCreate(run, Location);
                var nsg = Assert.IsType<NetworkSecurityGroup>(Assert.Single(Ps(run, "New-AzNetworkSecurityGroup", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = NsgName,
                    ["Location"] = Location,
                })));
                var pip = Assert.IsType<PublicIPAddress>(Assert.Single(Ps(run, "New-AzPublicIpAddress", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = PipName,
                    ["Location"] = Location,
                    ["Sku"] = "Standard",
                    ["AllocationMethod"] = "Static",
                })));

                var first = Single(Ps(run, "New-AzNetworkInterface", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = NicName,
                    ["Location"] = Location,
                    ["SubnetId"] = subnetId,
                    ["NetworkSecurityGroupId"] = nsg.Id!,
                    ["EnableIPForwarding"] = true,
                }));
                Assert.Equal(nsg.Id, first.NetworkSecurityGroupId, ignoreCase: true);
                Assert.True(first.EnableIPForwarding);
                Assert.Equal("Dynamic", first.IpConfigurations![0].PrivateIPAllocationMethod);

                int sent = run.Traffic.Sent.Count;
                var second = Single(Ps(run, "New-AzNetworkInterface", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = NicName,
                    ["Location"] = Location,
                    ["SubnetId"] = subnetId,
                    ["NetworkSecurityGroupId"] = nsg.Id!,
                    ["PublicIpAddressId"] = pip.Id!,
                }));
                AssertRecreated(PutBody(run, sent)["properties"]!, second);
                Assert.Equal(pip.Id, second.IpConfigurations![0].PublicIpAddressId, ignoreCase: true);
                Assert.Equal(nsg.Id, second.NetworkSecurityGroupId, ignoreCase: true);

                PsGroupDelete(run);
            });
    }

    private static void AssertRecreated(System.Text.Json.Nodes.JsonNode properties, NetworkInterface nic)
    {
        Assert.Equal("Succeeded", nic.ProvisioningState);
        Assert.Null(properties["enableIPForwarding"]);
        Assert.False(nic.EnableIPForwarding);
        Assert.EndsWith($"/networkSecurityGroups/{NsgName}", nic.NetworkSecurityGroupId, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith($"/publicIPAddresses/{PipName}", nic.IpConfigurations![0].PublicIpAddressId, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Dynamic", nic.IpConfigurations[0].PrivateIPAllocationMethod);
    }
}
