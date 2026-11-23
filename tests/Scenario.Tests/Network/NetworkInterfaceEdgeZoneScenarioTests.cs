using Microsoft.Azure.Azclips.Extensions.Network.Models;
using static Scenario.Tests.NetworkInterfaceScenario;

namespace Scenario.Tests;

/// <summary>
/// Network interface in an edge zone: <c>--edge-zone</c> / <c>-EdgeZone</c> becomes the extended location.
/// Test spec: <c>network-network-interfaces-tests</c>.
/// </summary>
[Collection("Scenario Tests")]
public class NetworkInterfaceEdgeZoneScenarioTests
{
    private const string Location = "westus";
    private const string EdgeZone = "microsoftlosangeles1";
    private const string SubnetPrefix = "10.0.0.0/24";

    // T4, test spec scenario "T4 Create a network interface in an edge zone" (requirement "Edge zone as extended location").
    [Fact]
    public void NetworkInterfaceInAnEdgeZone()
    {
        RunCase(
            typeof(NetworkInterfaceEdgeZoneScenarioTests),
            nameof(NetworkInterfaceInAnEdgeZone),
            "scenario-nic-edgezone-rg",
            cli: run =>
            {
                CliGroupCreate(run, Location);
                CliVnetCreate(run, Location, "--extended-location", EdgeZone);
                var subnet = CliSubnetCreate(run, SubnetPrefix);

                int sent = run.Traffic.Sent.Count;
                var created = Single(Cli(run, "nic", "create", "-g", run.ResourceGroup, "-n", NicName, "-l", Location,
                    "--subnet", subnet.Id!, "--edge-zone", EdgeZone));
                AssertSent(run, sent);
                AssertInEdgeZone(created);
                AssertInEdgeZone(CliShow(run));

                CliGroupDelete(run);
            },
            cliSubnet: run => CliSubnetCreate(run, SubnetPrefix),
            powerShell: (run, subnetId) =>
            {
                PsGroupCreate(run, Location);
                PsVnetCreate(run, Location, EdgeZone);

                int sent = run.Traffic.Sent.Count;
                var created = Single(Ps(run, "New-AzNetworkInterface", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = NicName,
                    ["Location"] = Location,
                    ["EdgeZone"] = EdgeZone,
                    ["SubnetId"] = subnetId,
                }));
                AssertSent(run, sent);
                AssertInEdgeZone(created);
                AssertInEdgeZone(PsShow(run));

                PsGroupDelete(run);
            });
    }

    private static void AssertSent(NetworkInterfaceRun run, int sentBefore)
    {
        var extendedLocation = PutBody(run, sentBefore)["extendedLocation"]!;
        Assert.Equal(EdgeZone, extendedLocation["name"]!.GetValue<string>());
        Assert.Equal("EdgeZone", extendedLocation["type"]!.GetValue<string>());
    }

    private static void AssertInEdgeZone(NetworkInterface nic)
    {
        Assert.Equal(EdgeZone, nic.ExtendedLocationName);
        Assert.Equal("EdgeZone", nic.ExtendedLocationType);
    }
}
