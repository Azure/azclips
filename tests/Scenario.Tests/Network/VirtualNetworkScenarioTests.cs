using System.Text.Json.Nodes;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// Virtual network scenarios for <c>network vnet create/show/list/delete</c> and
/// <c>New-AzVirtualNetwork</c>, <c>Get-AzVirtualNetwork_Get</c>, <c>Get-AzVirtualNetwork_List</c>,
/// <c>Remove-AzVirtualNetwork</c>. Each case creates its own resource group and deletes it at the end.
/// Record mode runs the CLI steps once against live Azure; playback replays the one recording through
/// the CLI steps and then the PowerShell steps, which send the same requests.
/// Test spec: <c>network-virtual-networks-tests</c>.
/// </summary>
[Collection("Scenario Tests")]
public class VirtualNetworkScenarioTests
{
    private const string SubscriptionId = RecordingSanitizers.PlaceholderGuid;
    private const string VirtualNetworkType = "Microsoft.Network/virtualNetworks";
    private const string VirtualNetworksPath = "/providers/Microsoft.Network/virtualNetworks";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Network];

    /// <summary>One surface run of a case. <see cref="SubscriptionId"/> is known only in playback.</summary>
    private sealed record Run(ScenarioHost Host, RecordPlaybackInterceptor Traffic, string ResourceGroup, string? SubscriptionId);

    // T1, test spec scenario "T1 Virtual network lifecycle with DNS server and subnet".
    [Fact]
    public void VirtualNetworkLifecycleWithDnsServerAndSubnet()
    {
        const string resourceGroup = "scenario-vnet-lifecycle-rg";
        const string location = "westus";

        RunCase(
            nameof(VirtualNetworkLifecycleWithDnsServerAndSubnet),
            resourceGroup,
            cli: run =>
            {
                CliGroupCreate(run, location);

                int sent = run.Traffic.Sent.Count;
                AssertLifecycleCreated(Single(Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet1",
                    "--location", location, "--address-prefixes", "10.0.0.0/16", "--dns-servers", "8.8.8.8",
                    "--subnet-names", "subnet1", "--subnet-address-prefixes", "10.0.1.0/24")));
                JsonNode subnet = PutBody(run, sent, "vnet1")["properties"]!["subnets"]![0]!;
                Assert.Equal("subnet1", subnet["name"]!.GetValue<string>());
                Assert.Equal("10.0.1.0/24", subnet["properties"]!["addressPrefix"]!.GetValue<string>());

                AssertLifecycleShown(Single(Cli(run, "vnet", "show", "--resource-group", run.ResourceGroup, "--name", "vnet1")));

                sent = run.Traffic.Sent.Count;
                AssertListedInGroup(Items(Cli(run, "vnet", "list", "--resource-group", run.ResourceGroup)));
                Assert.Contains($"/resourceGroups/{run.ResourceGroup}{VirtualNetworksPath}", FirstGetPath(run, sent), StringComparison.OrdinalIgnoreCase);

                sent = run.Traffic.Sent.Count;
                AssertListedInSubscription(Items(Cli(run, "vnet", "list")));
                AssertSubscriptionListPath(run, FirstGetPath(run, sent));

                AssertNoVirtualNetwork(Cli(run, "vnet", "delete", "--resource-group", run.ResourceGroup, "--name", "vnet1"));
                Assert.Empty(Items(Cli(run, "vnet", "list", "--resource-group", run.ResourceGroup)));

                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, location);

                AssertLifecycleCreated(Single(Ps(run, "New-AzVirtualNetwork", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = "vnet1",
                    ["Location"] = location,
                    ["AddressPrefix"] = new[] { "10.0.0.0/16" },
                    ["DnsServer"] = new[] { "8.8.8.8" },
                    ["SubnetNames"] = new List<string> { "subnet1" },
                    ["SubnetAddressPrefixes"] = new List<List<string>> { new() { "10.0.1.0/24" } },
                })));

                AssertLifecycleShown(Single(PsGet(run, "vnet1")));
                AssertListedInGroup(Items(Ps(run, "Get-AzVirtualNetwork_List", new() { ["ResourceGroupName"] = run.ResourceGroup })));
                AssertListedInSubscription(Items(Ps(run, "Get-AzVirtualNetwork_List", new())));

                AssertNoVirtualNetwork(Ps(run, "Remove-AzVirtualNetwork", new() { ["ResourceGroupName"] = run.ResourceGroup, ["Name"] = "vnet1" }));
                Assert.Empty(Items(Ps(run, "Get-AzVirtualNetwork_List", new() { ["ResourceGroupName"] = run.ResourceGroup })));

                PsGroupDelete(run);
            },
            // The subscription-wide list returns every virtual network in the recording subscription;
            // keep only this case's group so nobody else's networks reach the committed recording.
            sanitizers: RecordingSanitizers.Default.With([new ResourceGroupListSanitizer(resourceGroup)]));

        static void AssertLifecycleCreated(VirtualNetwork vnet)
        {
            Assert.Equal("vnet1", vnet.Name);
            Assert.Equal("Succeeded", vnet.ProvisioningState);
            Assert.Equal(["10.0.0.0/16"], vnet.AddressSpaceAddressPrefixes!);
            Assert.Equal(["8.8.8.8"], vnet.DhcpOptionsDnsServers!);
            AssertSingleSubnet(vnet, "subnet1", "10.0.1.0/24");
        }

        static void AssertLifecycleShown(VirtualNetwork vnet)
        {
            Assert.Equal("vnet1", vnet.Name);
            Assert.Equal(VirtualNetworkType, vnet.Type);
            Assert.Equal("10.0.0.0/16", vnet.AddressSpaceAddressPrefixes![0]);
            Assert.Equal("8.8.8.8", vnet.DhcpOptionsDnsServers![0]);
            AssertSingleSubnet(vnet, "subnet1", "10.0.1.0/24");
        }

        static void AssertListedInGroup(IReadOnlyList<VirtualNetwork> vnets)
        {
            Assert.Equal("vnet1", Assert.Single(vnets).Name);
            Assert.All(vnets, v => Assert.Equal(VirtualNetworkType, v.Type));
        }

        static void AssertListedInSubscription(IReadOnlyList<VirtualNetwork> vnets)
        {
            Assert.Contains(vnets, v => v.Name == "vnet1");
            Assert.All(vnets, v => Assert.Equal(VirtualNetworkType, v.Type));
        }
    }

    // T2, test spec scenario "T2 Create defaults the address space to 10.0.0.0/16".
    [Fact]
    public void CreateDefaultsAddressSpaceTo10_0_0_0_16()
    {
        const string location = "westus";

        RunCase(
            nameof(CreateDefaultsAddressSpaceTo10_0_0_0_16),
            "scenario-vnet-default-prefix-rg",
            cli: run =>
            {
                CliGroupCreate(run, location);
                int sent = run.Traffic.Sent.Count;
                VirtualNetwork created = Single(Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet1",
                    "--location", location, "--subnet-names", "default", "--subnet-address-prefixes", "10.0.0.0/24"));
                AssertDefaultPrefixSent(PutBody(run, sent, "vnet1"));
                AssertDefaultPrefixCreated(created);
                Assert.Equal(["10.0.0.0/16"], Single(Cli(run, "vnet", "show", "--resource-group", run.ResourceGroup, "--name", "vnet1")).AddressSpaceAddressPrefixes!);
                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, location);
                int sent = run.Traffic.Sent.Count;
                VirtualNetwork created = Single(Ps(run, "New-AzVirtualNetwork", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = "vnet1",
                    ["Location"] = location,
                    ["SubnetNames"] = new List<string> { "default" },
                    ["SubnetAddressPrefixes"] = new List<List<string>> { new() { "10.0.0.0/24" } },
                }));
                AssertDefaultPrefixSent(PutBody(run, sent, "vnet1"));
                AssertDefaultPrefixCreated(created);
                Assert.Equal(["10.0.0.0/16"], Single(PsGet(run, "vnet1")).AddressSpaceAddressPrefixes!);
                PsGroupDelete(run);
            });

        static void AssertDefaultPrefixSent(JsonNode body)
        {
            var prefixes = body["properties"]!["addressSpace"]!["addressPrefixes"]!.AsArray().Select(p => p!.GetValue<string>());
            Assert.Equal(["10.0.0.0/16"], prefixes);
        }

        static void AssertDefaultPrefixCreated(VirtualNetwork vnet)
        {
            Assert.Equal("Succeeded", vnet.ProvisioningState);
            Assert.Equal(["10.0.0.0/16"], vnet.AddressSpaceAddressPrefixes!);
            Assert.Equal("default", vnet.Subnets![0].Name);
            Assert.Equal("Disabled", vnet.Subnets[0].PrivateEndpointNetworkPolicies);
        }
    }

    // T3, test spec scenario "T3 Create with flow timeout".
    [Fact]
    public void CreateWithFlowTimeout()
    {
        const string location = "westus";

        RunCase(
            nameof(CreateWithFlowTimeout),
            "scenario-vnet-flowtimeout-rg",
            cli: run =>
            {
                CliGroupCreate(run, location);
                Assert.Equal(10, Single(Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet",
                    "--location", location, "--flowtimeout", "10")).FlowTimeoutInMinutes);
                Assert.Equal(10, Single(Cli(run, "vnet", "show", "--resource-group", run.ResourceGroup, "--name", "vnet")).FlowTimeoutInMinutes);
                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, location);
                Assert.Equal(10, Single(Ps(run, "New-AzVirtualNetwork", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = "vnet",
                    ["Location"] = location,
                    ["FlowTimeout"] = 10,
                })).FlowTimeoutInMinutes);
                Assert.Equal(10, Single(PsGet(run, "vnet")).FlowTimeoutInMinutes);
                PsGroupDelete(run);
            });
    }

    // T4, test spec scenario "T4 Create with BGP community".
    [Fact]
    public void CreateWithBgpCommunity()
    {
        const string location = "westus";
        const string community = "12076:20000";

        RunCase(
            nameof(CreateWithBgpCommunity),
            "scenario-vnet-bgp-rg",
            cli: run =>
            {
                CliGroupCreate(run, location);
                Assert.Equal(community, Single(Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet1",
                    "--location", location, "--bgp-community", community)).BgpCommunitiesVirtualNetworkCommunity);
                Assert.Equal(community, Single(Cli(run, "vnet", "show", "--resource-group", run.ResourceGroup, "--name", "vnet1")).BgpCommunitiesVirtualNetworkCommunity);
                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, location);
                Assert.Equal(community, Single(Ps(run, "New-AzVirtualNetwork", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = "vnet1",
                    ["Location"] = location,
                    ["BgpCommunity"] = community,
                })).BgpCommunitiesVirtualNetworkCommunity);
                Assert.Equal(community, Single(PsGet(run, "vnet1")).BgpCommunitiesVirtualNetworkCommunity);
                PsGroupDelete(run);
            });
    }

    // T5, test spec scenario "T5 Create with encryption settings".
    [Fact]
    public void CreateWithEncryptionSettings()
    {
        const string location = "westus";

        RunCase(
            nameof(CreateWithEncryptionSettings),
            "scenario-vnet-encryption-rg",
            cli: run =>
            {
                CliGroupCreate(run, location);
                Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "MyVnet1", "--location", location,
                    "--address-prefixes", "10.0.0.0/16", "--subnet-names", "Mysubnet", "--subnet-address-prefixes", "10.0.0.0/24",
                    "--enable-encryption", "true", "--encryption-policy", "AllowUnencrypted");
                Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "MyVnet2", "--location", location,
                    "--address-prefixes", "10.1.0.0/16", "--subnet-names", "MySubnet", "--subnet-address-prefixes", "10.1.0.0/24",
                    "--enable-encryption", "true", "--encryption-policy", "DropUnencrypted");
                Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "MyVnet3", "--location", location,
                    "--address-prefixes", "10.2.0.0/16", "--subnet-names", "Mysubnet", "--subnet-address-prefixes", "10.2.0.0/24",
                    "--enable-encryption", "false");
                AssertEncryption(Single(Cli(run, "vnet", "show", "--resource-group", run.ResourceGroup, "--name", "MyVnet1")), true, "AllowUnencrypted");
                AssertEncryption(Single(Cli(run, "vnet", "show", "--resource-group", run.ResourceGroup, "--name", "MyVnet2")), true, "DropUnencrypted");
                AssertEncryption(Single(Cli(run, "vnet", "show", "--resource-group", run.ResourceGroup, "--name", "MyVnet3")), false);
                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, location);
                Ps(run, "New-AzVirtualNetwork", EncryptedVnet(run, "MyVnet1", "10.0", "Mysubnet", true, "AllowUnencrypted"));
                Ps(run, "New-AzVirtualNetwork", EncryptedVnet(run, "MyVnet2", "10.1", "MySubnet", true, "DropUnencrypted"));
                Ps(run, "New-AzVirtualNetwork", EncryptedVnet(run, "MyVnet3", "10.2", "Mysubnet", false, null));
                AssertEncryption(Single(PsGet(run, "MyVnet1")), true, "AllowUnencrypted");
                AssertEncryption(Single(PsGet(run, "MyVnet2")), true, "DropUnencrypted");
                AssertEncryption(Single(PsGet(run, "MyVnet3")), false);
                PsGroupDelete(run);
            });

        static Dictionary<string, object> EncryptedVnet(Run run, string name, string network, string subnet, bool enabled, string? policy)
        {
            var parameters = new Dictionary<string, object>
            {
                ["ResourceGroupName"] = run.ResourceGroup,
                ["Name"] = name,
                ["Location"] = location,
                ["AddressPrefix"] = new[] { $"{network}.0.0/16" },
                ["SubnetNames"] = new List<string> { subnet },
                ["SubnetAddressPrefixes"] = new List<List<string>> { new() { $"{network}.0.0/24" } },
                ["EnableEncryption"] = enabled,
            };
            if (policy is not null)
            {
                parameters["EncryptionEnforcementPolicy"] = policy;
            }
            return parameters;
        }

        static void AssertEncryption(VirtualNetwork vnet, bool enabled, string? enforcement = null)
        {
            Assert.Equal(enabled, vnet.EncryptionEnabled);
            if (enforcement is not null)
            {
                Assert.Equal(enforcement, vnet.EncryptionEnforcement);
            }
        }
    }

    // T6, test spec scenario "T6 Create with private endpoint VNet policies".
    [Fact]
    public void CreateWithPrivateEndpointVNetPolicies()
    {
        const string location = "westus";

        RunCase(
            nameof(CreateWithPrivateEndpointVNetPolicies),
            "scenario-vnet-pe-policies-rg",
            cli: run =>
            {
                CliGroupCreate(run, location);
                Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet1", "--location", location, "--address-prefixes", "10.1.0.0/16");
                Assert.Equal("Disabled", Single(Cli(run, "vnet", "show", "--resource-group", run.ResourceGroup, "--name", "vnet1")).PrivateEndpointVNetPolicies);

                int sent = run.Traffic.Sent.Count;
                Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet2", "--location", location,
                    "--address-prefixes", "10.2.0.0/16", "--pe-vnet-policies", "Disabled");
                Assert.Equal("Disabled", PutBody(run, sent, "vnet2")["properties"]!["privateEndpointVNetPolicies"]!.GetValue<string>());
                Assert.Equal("Disabled", Single(Cli(run, "vnet", "show", "--resource-group", run.ResourceGroup, "--name", "vnet2")).PrivateEndpointVNetPolicies);

                Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet3", "--location", location,
                    "--address-prefixes", "10.3.0.0/16", "--pe-vnet-policies", "Basic");
                Assert.Equal("Basic", Single(Cli(run, "vnet", "show", "--resource-group", run.ResourceGroup, "--name", "vnet3")).PrivateEndpointVNetPolicies);
                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, location);
                Ps(run, "New-AzVirtualNetwork", PolicyVnet(run, "vnet1", "10.1.0.0/16", null));
                Assert.Equal("Disabled", Single(PsGet(run, "vnet1")).PrivateEndpointVNetPolicies);

                int sent = run.Traffic.Sent.Count;
                Ps(run, "New-AzVirtualNetwork", PolicyVnet(run, "vnet2", "10.2.0.0/16", "Disabled"));
                Assert.Equal("Disabled", PutBody(run, sent, "vnet2")["properties"]!["privateEndpointVNetPolicies"]!.GetValue<string>());
                Assert.Equal("Disabled", Single(PsGet(run, "vnet2")).PrivateEndpointVNetPolicies);

                Ps(run, "New-AzVirtualNetwork", PolicyVnet(run, "vnet3", "10.3.0.0/16", "Basic"));
                Assert.Equal("Basic", Single(PsGet(run, "vnet3")).PrivateEndpointVNetPolicies);
                PsGroupDelete(run);
            });

        static Dictionary<string, object> PolicyVnet(Run run, string name, string prefix, string? policy)
        {
            var parameters = new Dictionary<string, object>
            {
                ["ResourceGroupName"] = run.ResourceGroup,
                ["Name"] = name,
                ["Location"] = location,
                ["AddressPrefix"] = new[] { prefix },
            };
            if (policy is not null)
            {
                parameters["PrivateEndpointVNetPoliciesValue"] = policy;
            }
            return parameters;
        }
    }

    // T7, test spec scenario "T7 Create with summarized gateway prefixes".
    // Recording needs access to the centraluseuap region.
    [Fact]
    public void CreateWithSummarizedGatewayPrefixes()
    {
        const string location = "centraluseuap";

        RunCase(
            nameof(CreateWithSummarizedGatewayPrefixes),
            "scenario-vnet-sgp-rg",
            cli: run =>
            {
                CliGroupCreate(run, location);
                VirtualNetwork created = Single(Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet1",
                    "--location", location, "--address-prefixes", "10.0.0.0/16", "--sgp", "10.0.0.0/16"));
                Assert.Equal("Succeeded", created.ProvisioningState);
                Assert.Equal("10.0.0.0/16", created.SummarizedGatewayPrefixesAddressPrefixes![0]);
                Assert.Equal("10.0.0.0/16", Single(Cli(run, "vnet", "show", "--resource-group", run.ResourceGroup, "--name", "vnet1")).SummarizedGatewayPrefixesAddressPrefixes![0]);
                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, location);
                VirtualNetwork created = Single(Ps(run, "New-AzVirtualNetwork", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = "vnet1",
                    ["Location"] = location,
                    ["AddressPrefix"] = new[] { "10.0.0.0/16" },
                    ["SummarizedGatewayPrefix"] = new[] { "10.0.0.0/16" },
                }));
                Assert.Equal("Succeeded", created.ProvisioningState);
                Assert.Equal("10.0.0.0/16", created.SummarizedGatewayPrefixesAddressPrefixes![0]);
                Assert.Equal("10.0.0.0/16", Single(PsGet(run, "vnet1")).SummarizedGatewayPrefixesAddressPrefixes![0]);
                PsGroupDelete(run);
            });
    }

    // T8, test spec scenario "T8 Create in an edge zone".
    // Recording needs access to the eastus2euap region and the MicrosoftRRDCLab1 edge zone.
    [Fact]
    public void CreateInEdgeZone()
    {
        const string location = "eastus2euap";
        const string edgeZone = "MicrosoftRRDCLab1";

        RunCase(
            nameof(CreateInEdgeZone),
            "scenario-vnet-edgezone-rg",
            cli: run =>
            {
                CliGroupCreate(run, location);
                int sent = run.Traffic.Sent.Count;
                Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet1", "--location", location,
                    "--address-prefixes", "10.0.0.0/16", "--dns-servers", "8.8.8.8", "--subnet-names", "subnet1",
                    "--subnet-address-prefixes", "10.0.1.0/24", "--extended-location", edgeZone);
                AssertEdgeZoneSent(PutBody(run, sent, "vnet1"));
                AssertEdgeZone(Single(Cli(run, "vnet", "show", "--resource-group", run.ResourceGroup, "--name", "vnet1")));
                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, location);
                int sent = run.Traffic.Sent.Count;
                Ps(run, "New-AzVirtualNetwork", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = "vnet1",
                    ["Location"] = location,
                    ["AddressPrefix"] = new[] { "10.0.0.0/16" },
                    ["DnsServer"] = new[] { "8.8.8.8" },
                    ["SubnetNames"] = new List<string> { "subnet1" },
                    ["SubnetAddressPrefixes"] = new List<List<string>> { new() { "10.0.1.0/24" } },
                    ["EdgeZone"] = edgeZone,
                });
                AssertEdgeZoneSent(PutBody(run, sent, "vnet1"));
                AssertEdgeZone(Single(PsGet(run, "vnet1")));
                PsGroupDelete(run);
            });

        static void AssertEdgeZoneSent(JsonNode body)
        {
            Assert.Equal(edgeZone, body["extendedLocation"]!["name"]!.GetValue<string>());
            Assert.Equal("EdgeZone", body["extendedLocation"]!["type"]!.GetValue<string>());
        }

        static void AssertEdgeZone(VirtualNetwork vnet)
        {
            Assert.Equal(edgeZone, vnet.ExtendedLocationName);
            Assert.Equal("EdgeZone", vnet.ExtendedLocationType);
        }
    }

    /// <summary>
    /// Record: runs the CLI steps once against live Azure with a run-scoped group name, then saves.
    /// Playback: replays the recording through the CLI steps, then through the PowerShell steps.
    /// </summary>
    private static void RunCase(string testName, string resourceGroup, Action<Run> cli, Action<Run> powerShell, RecordingSanitizers? sanitizers = null)
    {
        var recording = SessionRecordFiles.OpenSession(typeof(VirtualNetworkScenarioTests), testName, sanitizers: sanitizers);

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(resourceGroup, recording.Mode);
            var traffic = recording.CreateInterceptor();
            using var host = new ScenarioHost(ClientType.CLI, traffic, useLiveAuth: true, extensions: s_extensions);
            try
            {
                cli(new Run(host, traffic, groupName, null));
                recording.Save();
            }
            finally
            {
                // After the save, so this safety delete never lands in the recording.
                try { host.RunCli("group", "delete", "--name", groupName); } catch { }
            }
            return;
        }

        var cliTraffic = recording.CreateInterceptor();
        using (var host = new ScenarioHost(ClientType.CLI, cliTraffic, extensions: s_extensions))
        {
            cli(new Run(host, cliTraffic, resourceGroup, SubscriptionId));
        }

        var psTraffic = recording.CreateInterceptor();
        using (var host = new ScenarioHost(ClientType.PowerShell, psTraffic, extensions: s_extensions))
        {
            powerShell(new Run(host, psTraffic, resourceGroup, SubscriptionId));
        }
    }

    private static IReadOnlyList<object> Cli(Run run, params string[] args) => run.Host.RunCli(["network", .. args]);

    private static IReadOnlyList<object> Ps(Run run, string command, Dictionary<string, object> parameters) =>
        run.Host.RunPowerShell(command, parameters, extension: "Network");

    private static IReadOnlyList<object> PsGet(Run run, string name) =>
        Ps(run, "Get-AzVirtualNetwork_Get", new() { ["ResourceGroupName"] = run.ResourceGroup, ["Name"] = name });

    private static void CliGroupCreate(Run run, string location) =>
        run.Host.RunCli("group", "create", "--name", run.ResourceGroup, "--location", location);

    private static void CliGroupDelete(Run run) => run.Host.RunCli("group", "delete", "--name", run.ResourceGroup);

    private static void PsGroupCreate(Run run, string location) =>
        run.Host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object> { ["Name"] = run.ResourceGroup, ["Location"] = location }, extension: "Resources");

    private static void PsGroupDelete(Run run) =>
        run.Host.RunPowerShell("Remove-AzResourceGroup", new Dictionary<string, object> { ["Name"] = run.ResourceGroup }, extension: "Resources");

    private static VirtualNetwork Single(IReadOnlyList<object> outputs) => Assert.IsType<VirtualNetwork>(Assert.Single(outputs));

    /// <summary>List output arrives either item by item or as one page collection; flatten both.</summary>
    private static List<VirtualNetwork> Items(IReadOnlyList<object> outputs) =>
        [.. outputs.SelectMany(output => output switch
        {
            VirtualNetwork vnet => [vnet],
            IEnumerable<VirtualNetwork> page => page,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected list output {output.GetType()}."),
        })];

    private static void AssertNoVirtualNetwork(IReadOnlyList<object> outputs) =>
        Assert.DoesNotContain(outputs, output => output is VirtualNetwork);

    private static void AssertSingleSubnet(VirtualNetwork vnet, string name, string addressPrefix)
    {
        var subnet = Assert.Single(vnet.Subnets!);
        Assert.Equal(name, subnet.Name);
        Assert.Equal(addressPrefix, subnet.AddressPrefix);
    }

    /// <summary>The body of the PUT this step sent to the named virtual network.</summary>
    private static JsonNode PutBody(Run run, int sentBefore, string name)
    {
        var put = run.Traffic.Sent.Skip(sentBefore).Single(request => request.Method == "PUT"
            && new Uri(request.Uri).AbsolutePath.EndsWith($"{VirtualNetworksPath}/{name}", StringComparison.OrdinalIgnoreCase));
        return JsonNode.Parse(put.Body!)!;
    }

    /// <summary>The path of the first GET this step sent; later GETs are next-page links.</summary>
    private static string FirstGetPath(Run run, int sentBefore) =>
        new Uri(run.Traffic.Sent.Skip(sentBefore).First(request => request.Method == "GET").Uri).AbsolutePath;

    // The subscription is the developer's while recording, so its exact value is asserted only in playback.
    private static void AssertSubscriptionListPath(Run run, string path)
    {
        Assert.DoesNotContain("/resourceGroups/", path, StringComparison.OrdinalIgnoreCase);
        if (run.SubscriptionId is not null)
        {
            Assert.Equal($"/subscriptions/{run.SubscriptionId}{VirtualNetworksPath}", path, ignoreCase: true);
        }
        else
        {
            Assert.StartsWith("/subscriptions/", path, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(VirtualNetworksPath, path, StringComparison.OrdinalIgnoreCase);
        }
    }
}
