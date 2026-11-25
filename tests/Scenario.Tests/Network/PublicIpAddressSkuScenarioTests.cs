using System.Text.Json.Nodes;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using static Scenario.Tests.PublicIpAddressScenario;

namespace Scenario.Tests;

/// <summary>
/// Public IP address create defaults and options: SKU, tier, IP version, idle timeout, DDoS protection mode,
/// and allocation method. Each case uses the group <c>scenario-pip-sku-rg</c> and deletes it at the end.
/// Test spec: <c>network-public-ip-addresses-tests</c>.
/// </summary>
[Collection("Scenario Tests")]
public class PublicIpAddressSkuScenarioTests
{
    private const string ResourceGroup = "scenario-pip-sku-rg";
    private const string Location = "eastus2";

    // T2, test spec scenario "T2 SKU defaults, tags, Global tier, and StandardV2" (requirement "SKU default on create").
    [Fact]
    public void SkuDefaultsTagsGlobalTierAndStandardV2()
    {
        RunCase(
            typeof(PublicIpAddressSkuScenarioTests),
            nameof(SkuDefaultsTagsGlobalTierAndStandardV2),
            ResourceGroup,
            cli: run =>
            {
                CliGroupCreate(run, Location);

                int sent = run.Traffic.Sent.Count;
                var pubip1 = Single(Cli(run, "create", "-g", run.ResourceGroup, "-n", "pubip1", "-l", Location));
                AssertDefaultCreate(PutBody(run, sent, "pubip1"), pubip1);
                AssertDefaultShown(CliShow(run, "pubip1"));

                sent = run.Traffic.Sent.Count;
                Cli(run, "create", "-g", run.ResourceGroup, "-n", "pubip2", "-l", Location, "--sku", "Standard", "--tags", "foo=doo");
                AssertTagsSent(PutBody(run, sent, "pubip2"));
                AssertTaggedShown(CliShow(run, "pubip2"));

                sent = run.Traffic.Sent.Count;
                Cli(run, "create", "-g", run.ResourceGroup, "-n", "pubip3", "-l", Location, "--sku", "Standard", "--tier", "Global");
                AssertGlobalTierSent(PutBody(run, sent, "pubip3"));
                AssertGlobalShown(CliShow(run, "pubip3"));

                Assert.Equal("StandardV2", Single(Cli(run, "create", "-g", run.ResourceGroup, "-n", "pubip4", "-l", Location,
                    "--sku", "StandardV2", "--allocation-method", "Static")).SkuName);

                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, Location);

                int sent = run.Traffic.Sent.Count;
                var pubip1 = Single(Ps(run, "New-AzPublicIpAddress", Create(run, "pubip1")));
                AssertDefaultCreate(PutBody(run, sent, "pubip1"), pubip1);
                AssertDefaultShown(PsShow(run, "pubip1"));

                sent = run.Traffic.Sent.Count;
                Ps(run, "New-AzPublicIpAddress", Create(run, "pubip2", ("Sku", "Standard"), ("Tag", new Dictionary<string, string> { ["foo"] = "doo" })));
                AssertTagsSent(PutBody(run, sent, "pubip2"));
                AssertTaggedShown(PsShow(run, "pubip2"));

                sent = run.Traffic.Sent.Count;
                Ps(run, "New-AzPublicIpAddress", Create(run, "pubip3", ("Sku", "Standard"), ("Tier", "Global")));
                AssertGlobalTierSent(PutBody(run, sent, "pubip3"));
                AssertGlobalShown(PsShow(run, "pubip3"));

                Assert.Equal("StandardV2", Single(Ps(run, "New-AzPublicIpAddress",
                    Create(run, "pubip4", ("Sku", "StandardV2"), ("AllocationMethod", "Static")))).SkuName);

                PsGroupDelete(run);
            });

        static void AssertDefaultCreate(JsonNode body, PublicIPAddress ip)
        {
            Assert.Equal("Standard", body["sku"]!["name"]!.GetValue<string>());
            Assert.Equal("Static", body["properties"]!["publicIPAllocationMethod"]!.GetValue<string>());
            Assert.Equal("IPv4", body["properties"]!["publicIPAddressVersion"]!.GetValue<string>());
            Assert.Equal(4, body["properties"]!["idleTimeoutInMinutes"]!.GetValue<int>());

            Assert.Equal("Standard", ip.SkuName);
            Assert.Equal("Regional", ip.SkuTier);
            Assert.Equal("Static", ip.PublicIPAllocationMethod);
            Assert.Null(ip.DnsSettingsDomainNameLabel);
        }

        static void AssertDefaultShown(PublicIPAddress ip)
        {
            Assert.Equal("Standard", ip.SkuName);
            Assert.Equal("Regional", ip.SkuTier);
            Assert.Equal("Static", ip.PublicIPAllocationMethod);
            Assert.Equal("IPv4", ip.PublicIPAddressVersion);
        }

        static void AssertTagsSent(JsonNode body)
        {
            var tags = body["tags"]!.AsObject();
            Assert.Single(tags);
            Assert.Equal("doo", tags["foo"]!.GetValue<string>());
        }

        static void AssertTaggedShown(PublicIPAddress ip)
        {
            Assert.Equal("Standard", ip.SkuName);
            Assert.Equal("Regional", ip.SkuTier);
            Assert.Equal("Static", ip.PublicIPAllocationMethod);
            Assert.Equal("doo", Assert.Contains("foo", ip.Tags!));
        }

        static void AssertGlobalTierSent(JsonNode body) =>
            Assert.Equal("Global", body["sku"]!["tier"]!.GetValue<string>());

        static void AssertGlobalShown(PublicIPAddress ip)
        {
            Assert.Equal("Standard", ip.SkuName);
            Assert.Equal("Global", ip.SkuTier);
            Assert.Equal("Static", ip.PublicIPAllocationMethod);
        }
    }

    // T3, test spec scenario "T3 IPv6 public IP address" (requirement "IP version default on create").
    [Fact]
    public void IPv6PublicIpAddress()
    {
        const string name = "pubipv6";

        RunCase(
            typeof(PublicIpAddressSkuScenarioTests),
            nameof(IPv6PublicIpAddress),
            ResourceGroup,
            cli: run =>
            {
                CliGroupCreate(run, Location);
                int sent = run.Traffic.Sent.Count;
                var created = Single(Cli(run, "create", "-g", run.ResourceGroup, "-n", name, "-l", Location,
                    "--allocation-method", "Static", "--version", "IPv6"));
                AssertCreated(PutBody(run, sent, name), created);
                AssertShown(CliShow(run, name));
                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, Location);
                int sent = run.Traffic.Sent.Count;
                var created = Single(Ps(run, "New-AzPublicIpAddress",
                    Create(run, name, ("AllocationMethod", "Static"), ("IpAddressVersion", "IPv6"))));
                AssertCreated(PutBody(run, sent, name), created);
                AssertShown(PsShow(run, name));
                PsGroupDelete(run);
            });

        static void AssertCreated(JsonNode body, PublicIPAddress ip)
        {
            Assert.Equal("IPv6", body["properties"]!["publicIPAddressVersion"]!.GetValue<string>());
            Assert.Equal("IPv6", ip.PublicIPAddressVersion);
            Assert.Equal("Static", ip.PublicIPAllocationMethod);
        }

        static void AssertShown(PublicIPAddress ip)
        {
            Assert.Equal("IPv6", ip.PublicIPAddressVersion);
            Assert.Equal("Succeeded", ip.ProvisioningState);
        }
    }

    // T4, test spec scenario "T4 Explicit idle timeout" (requirement "Idle timeout default on create").
    [Fact]
    public void ExplicitIdleTimeout()
    {
        const string name = "pubipidle";
        const string dnsLabel = "azclipspipdns2";

        RunCase(
            typeof(PublicIpAddressSkuScenarioTests),
            nameof(ExplicitIdleTimeout),
            ResourceGroup,
            cli: run =>
            {
                CliGroupCreate(run, Location);
                int sent = run.Traffic.Sent.Count;
                Cli(run, "create", "-g", run.ResourceGroup, "-n", name, "-l", Location,
                    "--idle-timeout", "15", "--allocation-method", "Static", "--dns-name", dnsLabel);
                AssertSent(PutBody(run, sent, name));
                AssertShown(CliShow(run, name));
                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, Location);
                int sent = run.Traffic.Sent.Count;
                Ps(run, "New-AzPublicIpAddress",
                    Create(run, name, ("IdleTimeoutInMinutes", 15), ("AllocationMethod", "Static"), ("DomainNameLabel", dnsLabel)));
                AssertSent(PutBody(run, sent, name));
                AssertShown(PsShow(run, name));
                PsGroupDelete(run);
            });

        static void AssertSent(JsonNode body) =>
            Assert.Equal(15, body["properties"]!["idleTimeoutInMinutes"]!.GetValue<int>());

        static void AssertShown(PublicIPAddress ip)
        {
            Assert.Equal(15, ip.IdleTimeoutInMinutes);
            Assert.Equal("Static", ip.PublicIPAllocationMethod);
            Assert.Equal(dnsLabel, ip.DnsSettingsDomainNameLabel);
        }
    }

    // T5, test spec scenario "T5 DDoS protection mode on create" (requirement "DDoS protection mode and plan").
    [Fact]
    public void DdosProtectionModeOnCreate()
    {
        const string name = "pubipddos";

        RunCase(
            typeof(PublicIpAddressSkuScenarioTests),
            nameof(DdosProtectionModeOnCreate),
            ResourceGroup,
            cli: run =>
            {
                CliGroupCreate(run, Location);
                int sent = run.Traffic.Sent.Count;
                Cli(run, "create", "-g", run.ResourceGroup, "-n", name, "-l", Location,
                    "--allocation-method", "Static", "--sku", "Standard", "--protection-mode", "Enabled");
                AssertSent(PutBody(run, sent, name));
                AssertShown(CliShow(run, name));
                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, Location);
                int sent = run.Traffic.Sent.Count;
                Ps(run, "New-AzPublicIpAddress",
                    Create(run, name, ("AllocationMethod", "Static"), ("Sku", "Standard"), ("DdosProtectionMode", "Enabled")));
                AssertSent(PutBody(run, sent, name));
                AssertShown(PsShow(run, name));
                PsGroupDelete(run);
            });

        static void AssertSent(JsonNode body)
        {
            var ddos = body["properties"]!["ddosSettings"]!;
            Assert.Equal("Enabled", ddos["protectionMode"]!.GetValue<string>());
            Assert.Null(ddos["ddosProtectionPlan"]);
        }

        static void AssertShown(PublicIPAddress ip)
        {
            Assert.Equal("Enabled", ip.DdosSettingsProtectionMode);
            Assert.Equal("Standard", ip.SkuName);
            Assert.Equal("Static", ip.PublicIPAllocationMethod);
        }
    }

    // T6, test spec scenario "T6 Basic SKU with Dynamic allocation" (requirement "Allocation method default from the SKU").
    // The service may reject Basic SKU creation at record time; see the change's review gaps.
    [Fact]
    public void BasicSkuWithDynamicAllocation()
    {
        const string name = "pubipbasic";
        const string dnsLabel = "azclipspipbasic";

        RunCase(
            typeof(PublicIpAddressSkuScenarioTests),
            nameof(BasicSkuWithDynamicAllocation),
            ResourceGroup,
            cli: run =>
            {
                CliGroupCreate(run, Location);
                int sent = run.Traffic.Sent.Count;
                var created = Single(Cli(run, "create", "-g", run.ResourceGroup, "-n", name, "-l", Location,
                    "--allocation-method", "Dynamic", "--sku", "Basic", "--dns-name", dnsLabel));
                AssertCreated(PutBody(run, sent, name), created);
                AssertShown(CliShow(run, name));
                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, Location);
                int sent = run.Traffic.Sent.Count;
                var created = Single(Ps(run, "New-AzPublicIpAddress",
                    Create(run, name, ("AllocationMethod", "Dynamic"), ("Sku", "Basic"), ("DomainNameLabel", dnsLabel))));
                AssertCreated(PutBody(run, sent, name), created);
                AssertShown(PsShow(run, name));
                PsGroupDelete(run);
            });

        static void AssertCreated(JsonNode body, PublicIPAddress ip)
        {
            Assert.Equal("Basic", body["sku"]!["name"]!.GetValue<string>());
            Assert.Equal("Dynamic", body["properties"]!["publicIPAllocationMethod"]!.GetValue<string>());
            Assert.Equal("Basic", ip.SkuName);
            Assert.Equal("Dynamic", ip.PublicIPAllocationMethod);
        }

        static void AssertShown(PublicIPAddress ip)
        {
            Assert.Equal("Basic", ip.SkuName);
            Assert.Equal("Dynamic", ip.PublicIPAllocationMethod);
            Assert.Equal(dnsLabel, ip.DnsSettingsDomainNameLabel);
        }
    }

    /// <summary>New-AzPublicIpAddress parameters for this case's group and location, plus <paramref name="extra"/>.</summary>
    private static Dictionary<string, object> Create(PublicIpAddressRun run, string name, params (string Key, object Value)[] extra)
    {
        var parameters = new Dictionary<string, object>
        {
            ["ResourceGroupName"] = run.ResourceGroup,
            ["Name"] = name,
            ["Location"] = Location,
        };
        foreach (var (key, value) in extra)
        {
            parameters[key] = value;
        }
        return parameters;
    }
}
