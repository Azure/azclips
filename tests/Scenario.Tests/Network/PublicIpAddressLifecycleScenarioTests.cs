using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Recording;
using static Scenario.Tests.PublicIpAddressScenario;

namespace Scenario.Tests;

/// <summary>
/// Public IP address lifecycle: create with DNS label scope, IP tags and zones, list in the group and in
/// the subscription, show, delete. Test spec: <c>network-public-ip-addresses-tests</c>.
/// </summary>
[Collection("Scenario Tests")]
public class PublicIpAddressLifecycleScenarioTests
{
    private const string Location = "eastus2";
    private const string DnsName = "pubipdns";
    private const string ZonalName = "pubip3";
    private const string DnsLabel = "azclipspipdns1";

    // T1, test spec scenario "T1 Public IP lifecycle: DNS label scope, IP tags, zones, list, show, delete"
    // (requirement "List scope by resource group presence").
    // T8, test spec scenario "T8 List public IP addresses in the subscription (synthesized)" (same requirement)
    // runs after T1 step 3 and shares this recording.
    [Fact]
    public void PublicIpLifecycleDnsLabelScopeIpTagsZonesListShowDelete()
    {
        const string resourceGroup = "scenario-pip-lifecycle-rg";

        RunCase(
            typeof(PublicIpAddressLifecycleScenarioTests),
            nameof(PublicIpLifecycleDnsLabelScopeIpTagsZonesListShowDelete),
            resourceGroup,
            cli: run =>
            {
                CliGroupCreate(run, Location);

                AssertDnsCreated(Single(Cli(run, "create", "-g", run.ResourceGroup, "-n", DnsName, "-l", Location,
                    "--dns-name", DnsLabel, "--dns-name-scope", "TenantReuse", "--allocation-method", "Static")));

                AssertZonalCreated(Single(Cli(run, "create", "-g", run.ResourceGroup, "-n", ZonalName, "-l", Location,
                    "--version", "IPv4", "--sku", "Standard", "--zone", "1", "2", "3", "--ip-tags", "RoutingPreference=Internet")));

                // T8
                int sent = run.Traffic.Sent.Count;
                var all = Items(Cli(run, "list"));
                AssertSubscriptionList(run, FirstGetPath(run, sent), all);

                AssertBothListed(Items(Cli(run, "list", "-g", run.ResourceGroup)));
                AssertShown(CliShow(run, DnsName));

                sent = run.Traffic.Sent.Count;
                Cli(run, "delete", "-g", run.ResourceGroup, "-n", DnsName);
                AssertDeleteSent(run, sent);

                AssertOnlyZonalLeft(Items(Cli(run, "list", "-g", run.ResourceGroup)));

                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, Location);

                AssertDnsCreated(Single(Ps(run, "New-AzPublicIpAddress", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = DnsName,
                    ["Location"] = Location,
                    ["DomainNameLabel"] = DnsLabel,
                    ["DomainNameLabelScope"] = "TenantReuse",
                    ["AllocationMethod"] = "Static",
                })));

                AssertZonalCreated(Single(Ps(run, "New-AzPublicIpAddress", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = ZonalName,
                    ["Location"] = Location,
                    ["IpAddressVersion"] = "IPv4",
                    ["Sku"] = "Standard",
                    ["Zone"] = new[] { "1", "2", "3" },
                    ["IpTag"] = new List<KeyValuePair<string, string>> { new("RoutingPreference", "Internet") },
                })));

                // T8
                int sent = run.Traffic.Sent.Count;
                var all = Items(Ps(run, "Get-AzPublicIpAddress_List", new()));
                AssertSubscriptionList(run, FirstGetPath(run, sent), all);

                AssertBothListed(Items(Ps(run, "Get-AzPublicIpAddress_List", new() { ["ResourceGroupName"] = run.ResourceGroup })));
                AssertShown(PsShow(run, DnsName));

                sent = run.Traffic.Sent.Count;
                Ps(run, "Remove-AzPublicIpAddress", new() { ["ResourceGroupName"] = run.ResourceGroup, ["Name"] = DnsName });
                AssertDeleteSent(run, sent);

                AssertOnlyZonalLeft(Items(Ps(run, "Get-AzPublicIpAddress_List", new() { ["ResourceGroupName"] = run.ResourceGroup })));

                PsGroupDelete(run);
            },
            // The subscription-wide list returns every public IP address in the recording subscription;
            // keep only this case's group so nobody else's addresses reach the committed recording.
            sanitizers: RecordingSanitizers.Default.With([new ResourceGroupListSanitizer(resourceGroup)]));
    }

    private static void AssertDnsCreated(PublicIPAddress ip)
    {
        Assert.Equal(DnsName, ip.Name);
        Assert.Equal("Static", ip.PublicIPAllocationMethod);
        Assert.Equal(DnsLabel, ip.DnsSettingsDomainNameLabel);
        Assert.Equal("TenantReuse", ip.DnsSettingsDomainNameLabelScope);
        Assert.Equal("Succeeded", ip.ProvisioningState);
    }

    private static void AssertZonalCreated(PublicIPAddress ip)
    {
        Assert.Equal(ZonalName, ip.Name);
        Assert.Equal("IPv4", ip.PublicIPAddressVersion);
        Assert.Equal("Static", ip.PublicIPAllocationMethod);
        Assert.Equal(["1", "2", "3"], (ip.Zones ?? []).Order(StringComparer.Ordinal));
        var tag = Assert.Single(ip.IpTags ?? []);
        Assert.Equal("RoutingPreference", tag.IpTagType);
        Assert.Equal("Internet", tag.Tag);
        Assert.Null(ip.DnsSettingsDomainNameLabel);
    }

    // T8: the list without a group is ListAll; the subscription is the developer's while recording,
    // so its exact value is asserted only in playback.
    private static void AssertSubscriptionList(PublicIpAddressRun run, string path, List<PublicIPAddress> all)
    {
        Assert.DoesNotContain("/resourceGroups/", path, StringComparison.OrdinalIgnoreCase);
        if (run.SubscriptionId is not null)
        {
            Assert.Equal($"/subscriptions/{run.SubscriptionId}{PublicIpAddressesPath}", path, ignoreCase: true);
        }
        else
        {
            Assert.StartsWith("/subscriptions/", path, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(PublicIpAddressesPath, path, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(all, ip => ip.Name == ZonalName);
    }

    private static void AssertBothListed(List<PublicIPAddress> items) =>
        Assert.Equal([DnsName, ZonalName], items.Select(ip => ip.Name).Order(StringComparer.Ordinal));

    private static void AssertShown(PublicIPAddress ip)
    {
        Assert.Equal(DnsName, ip.Name);
        Assert.Equal(Location, ip.Location);
        Assert.Equal(DnsLabel, ip.DnsSettingsDomainNameLabel);
    }

    private static void AssertDeleteSent(PublicIpAddressRun run, int sentBefore) =>
        Assert.Contains(run.Traffic.Sent.Skip(sentBefore), request => request.Method == "DELETE"
            && new Uri(request.Uri).AbsolutePath.EndsWith($"/resourceGroups/{run.ResourceGroup}{PublicIpAddressesPath}/{DnsName}", StringComparison.OrdinalIgnoreCase));

    private static void AssertOnlyZonalLeft(List<PublicIPAddress> items)
    {
        Assert.Contains(items, ip => ip.Name == ZonalName);
        Assert.DoesNotContain(items, ip => ip.Name == DnsName);
    }
}
