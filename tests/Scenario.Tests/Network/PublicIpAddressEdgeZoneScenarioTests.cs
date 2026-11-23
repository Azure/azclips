using Microsoft.Azure.Azclips.Extensions.Network.Models;
using static Scenario.Tests.PublicIpAddressScenario;

namespace Scenario.Tests;

/// <summary>
/// Public IP address in an edge zone: <c>--edge-zone</c> / <c>-EdgeZone</c> becomes the extended location.
/// Test spec: <c>network-public-ip-addresses-tests</c>.
/// </summary>
[Collection("Scenario Tests")]
public class PublicIpAddressEdgeZoneScenarioTests
{
    // T7, test spec scenario "T7 Public IP in an edge zone" (requirement "Edge zone as extended location").
    [Fact]
    public void PublicIpInAnEdgeZone()
    {
        const string location = "westus";
        const string edgeZone = "microsoftlosangeles1";
        const string name = "pubipedge";

        RunCase(
            typeof(PublicIpAddressEdgeZoneScenarioTests),
            nameof(PublicIpInAnEdgeZone),
            "scenario-pip-edgezone-rg",
            cli: run =>
            {
                CliGroupCreate(run, location);
                int sent = run.Traffic.Sent.Count;
                Cli(run, "create", "-g", run.ResourceGroup, "-n", name, "-l", location, "--edge-zone", edgeZone, "--sku", "Standard");
                AssertSent(run, sent);
                AssertShown(CliShow(run, name));
                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, location);
                int sent = run.Traffic.Sent.Count;
                Ps(run, "New-AzPublicIpAddress", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = name,
                    ["Location"] = location,
                    ["EdgeZone"] = edgeZone,
                    ["Sku"] = "Standard",
                });
                AssertSent(run, sent);
                AssertShown(PsShow(run, name));
                PsGroupDelete(run);
            });

        void AssertSent(PublicIpAddressRun run, int sentBefore)
        {
            var extendedLocation = PutBody(run, sentBefore, name)["extendedLocation"]!;
            Assert.Equal(edgeZone, extendedLocation["name"]!.GetValue<string>());
            Assert.Equal("EdgeZone", extendedLocation["type"]!.GetValue<string>());
        }

        static void AssertShown(PublicIPAddress ip)
        {
            Assert.Equal(edgeZone, ip.ExtendedLocationName);
            Assert.Equal("EdgeZone", ip.ExtendedLocationType);
        }
    }
}
