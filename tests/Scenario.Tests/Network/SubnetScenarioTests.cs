using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// Subnet scenarios for <c>network vnet subnet create/show/list/delete</c>. The PowerShell surface exposes
/// no Subnets command, so every case is CLI-only. Each case creates its own resource group and deletes it at
/// the end. Record mode runs the CLI steps once against live Azure; playback replays them through the CLI.
/// Test spec: <c>network-subnets-tests</c>.
/// </summary>
[Collection("Scenario Tests")]
public class SubnetScenarioTests
{
    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Network];

    private sealed record Run(ScenarioHost Host, string ResourceGroup);

    // T1, test spec scenario "T1 Subnet lifecycle with one and several address prefixes".
    [Fact]
    public void SubnetLifecycleWithOneAndSeveralAddressPrefixes()
    {
        const string location = "westus";

        RunCase(nameof(SubnetLifecycleWithOneAndSeveralAddressPrefixes), "scenario-subnet-lifecycle-rg", run =>
        {
            CliGroupCreate(run, location);
            Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet1", "--location", location,
                "--address-prefixes", "20.0.0.0/16", "10.0.0.0/16");

            Subnet subnet1 = Single(SubnetCli(run, "create", "vnet1", "--name", "subnet1", "--address-prefixes", "20.0.0.0/24", "--default-outbound", "false"));
            Assert.Equal("subnet1", subnet1.Name);
            Assert.Equal("20.0.0.0/24", subnet1.AddressPrefix);
            Assert.True(subnet1.AddressPrefixes is null || subnet1.AddressPrefixes.Count == 0);
            Assert.False(subnet1.DefaultOutboundAccess);
            Assert.Equal("Disabled", subnet1.PrivateEndpointNetworkPolicies);
            Assert.Equal("Enabled", subnet1.PrivateLinkServiceNetworkPolicies);

            Subnet subnet2 = Single(SubnetCli(run, "create", "vnet1", "--name", "subnet2", "--address-prefixes", "10.0.1.0/28", "10.0.2.0/28", "--default-outbound", "false"));
            Assert.Equal("subnet2", subnet2.Name);
            Assert.Equal(["10.0.1.0/28", "10.0.2.0/28"], subnet2.AddressPrefixes!);
            Assert.Null(subnet2.AddressPrefix);

            var names = Items(SubnetCli(run, "list", "vnet1")).Select(s => s.Name).ToList();
            Assert.Contains("subnet1", names);
            Assert.Contains("subnet2", names);

            Subnet shown1 = Single(SubnetCli(run, "show", "vnet1", "--name", "subnet1"));
            Assert.Equal("subnet1", shown1.Name);
            Assert.Equal("20.0.0.0/24", shown1.AddressPrefix);

            Assert.Equal(["10.0.1.0/28", "10.0.2.0/28"], Single(SubnetCli(run, "show", "vnet1", "--name", "subnet2")).AddressPrefixes!);

            SubnetCli(run, "delete", "vnet1", "--name", "subnet1");
            SubnetCli(run, "delete", "vnet1", "--name", "subnet2");
            Assert.DoesNotContain(Items(SubnetCli(run, "list", "vnet1")), s => s.Name is "subnet1" or "subnet2");

            Cli(run, "vnet", "delete", "--resource-group", run.ResourceGroup, "--name", "vnet1");
            CliGroupDelete(run);
        });
    }

    // T2, test spec scenario "T2 Subnet with a network security group by name and by ID".
    [Fact]
    public void SubnetWithNetworkSecurityGroupByNameAndById()
    {
        const string location = "westus";

        RunCase(nameof(SubnetWithNetworkSecurityGroupByNameAndById), "scenario-subnet-nsg-rg", run =>
        {
            CliGroupCreate(run, location);
            var nsg = Assert.IsType<NetworkSecurityGroup>(Assert.Single(Cli(run, "nsg", "create", "--resource-group", run.ResourceGroup,
                "--name", "nsg", "--location", location)));
            Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet1", "--location", location,
                "--address-prefixes", "20.0.0.0/16", "10.0.0.0/16");

            AssertNsg(Single(SubnetCli(run, "create", "vnet1", "--name", "subnet1", "--address-prefixes", "20.0.0.0/24",
                "--network-security-group", "nsg", "--default-outbound", "false")));
            AssertNsg(Single(SubnetCli(run, "create", "vnet1", "--name", "subnet2", "--address-prefixes", "20.0.1.0/24",
                "--network-security-group", nsg.Id!, "--default-outbound", "false")));

            var names = Items(SubnetCli(run, "list", "vnet1")).Select(s => s.Name).ToList();
            Assert.Contains("subnet1", names);
            Assert.Contains("subnet2", names);

            Subnet shown = Single(SubnetCli(run, "show", "vnet1", "--name", "subnet1"));
            Assert.Equal("subnet1", shown.Name);
            AssertNsg(shown);

            SubnetCli(run, "delete", "vnet1", "--name", "subnet1");
            SubnetCli(run, "delete", "vnet1", "--name", "subnet2");
            CliGroupDelete(run);
        });

        static void AssertNsg(Subnet subnet) =>
            Assert.EndsWith("/networkSecurityGroups/nsg", subnet.NetworkSecurityGroupId, StringComparison.OrdinalIgnoreCase);
    }

    // T3, test spec scenario "T3 Subnet with a service endpoint".
    [Fact]
    public void SubnetWithServiceEndpoint()
    {
        const string location = "eastus2";

        RunCase(nameof(SubnetWithServiceEndpoint), "scenario-subnet-endpoint-rg", run =>
        {
            CliGroupCreate(run, location);
            Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet1", "--location", location);

            Subnet subnet = Single(SubnetCli(run, "create", "vnet1", "--name", "subnet1", "--address-prefixes", "10.0.1.0/24",
                "--service-endpoints", "Microsoft.Storage", "--default-outbound", "false"));
            Assert.Equal("Microsoft.Storage", Assert.Single(subnet.ServiceEndpoints!).Service);

            CliGroupDelete(run);
        });
    }

    // T4, test spec scenario "T4 Subnet delegation in dotted and slash form".
    // Recording may show that the service rejects the slash form of step 4.
    [Fact]
    public void SubnetDelegationInDottedAndSlashForm()
    {
        const string location = "westcentralus";

        RunCase(nameof(SubnetDelegationInDottedAndSlashForm), "scenario-subnet-delegation-rg", run =>
        {
            CliGroupCreate(run, location);
            Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet1", "--location", location);

            Subnet dotted = Single(SubnetCli(run, "create", "vnet1", "--name", "subnet1", "--address-prefixes", "10.0.0.0/24",
                "--delegations", "Microsoft.Web.serverFarms", "--default-outbound", "false"));
            Assert.Equal("Microsoft.Web/serverFarms", Assert.Single(dotted.Delegations!).ServiceName);

            Subnet slashed = Single(SubnetCli(run, "create", "vnet1", "--name", "subnet2", "--address-prefixes", "10.0.1.0/24",
                "--delegations", "Microsoft.Sql/managedInstances", "--default-outbound", "false"));
            Assert.Equal("Microsoft.Sql/managedInstances", Assert.Single(slashed.Delegations!).ServiceName);

            CliGroupDelete(run);
        });
    }

    // T5, test spec scenario "T5 Deprecated disable network policy flags".
    [Fact]
    public void DeprecatedDisableNetworkPolicyFlags()
    {
        const string location = "westus";

        RunCase(nameof(DeprecatedDisableNetworkPolicyFlags), "scenario-subnet-policies-rg", run =>
        {
            CliGroupCreate(run, location);
            Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "MyVnet", "--location", location);

            AssertPolicies(Single(SubnetCli(run, "create", "MyVnet", "--address-prefixes", "10.0.1.0/24", "--name", "MySubnet1",
                "--disable-private-endpoint-network-policies", "true", "--default-outbound", "false")), "10.0.1.0/24", "Disabled", "Enabled");
            AssertPolicies(Single(SubnetCli(run, "create", "MyVnet", "--address-prefixes", "10.0.2.0/24", "--name", "MySubnet2",
                "--disable-private-link-service-network-policies", "true", "--default-outbound", "false")), "10.0.2.0/24", "Disabled", "Disabled");
            AssertPolicies(Single(SubnetCli(run, "create", "MyVnet", "--address-prefixes", "10.0.3.0/24", "--name", "MySubnet3",
                "--disable-private-endpoint-network-policies", "true", "--disable-private-link-service-network-policies", "true",
                "--default-outbound", "false")), "10.0.3.0/24", "Disabled", "Disabled");

            CliGroupDelete(run);
        });

        static void AssertPolicies(Subnet subnet, string addressPrefix, string endpointPolicies, string linkServicePolicies)
        {
            Assert.Equal(addressPrefix, subnet.AddressPrefix);
            Assert.Equal(endpointPolicies, subnet.PrivateEndpointNetworkPolicies);
            Assert.Equal(linkServicePolicies, subnet.PrivateLinkServiceNetworkPolicies);
        }
    }

    // T6, test spec scenario "T6 Subnet default outbound access true and false".
    [Fact]
    public void SubnetDefaultOutboundAccessTrueAndFalse()
    {
        const string location = "westcentralus";

        RunCase(nameof(SubnetDefaultOutboundAccessTrueAndFalse), "scenario-subnet-outbound-rg", run =>
        {
            CliGroupCreate(run, location);
            Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet1", "--location", location);

            Assert.False(Single(SubnetCli(run, "create", "vnet1", "--name", "subnet1", "--address-prefixes", "10.0.3.0/24",
                "--default-outbound-access", "false")).DefaultOutboundAccess);
            Assert.True(Single(SubnetCli(run, "create", "vnet1", "--name", "subnet2", "--address-prefixes", "10.0.4.0/24",
                "--default-outbound-access", "true")).DefaultOutboundAccess);
            Assert.True(Single(SubnetCli(run, "show", "vnet1", "--name", "subnet2")).DefaultOutboundAccess);

            CliGroupDelete(run);
        });
    }

    // T7, test spec scenario "T7 Subnet sharing scope Tenant".
    [Fact]
    public void SubnetSharingScopeTenant()
    {
        const string location = "westcentralus";

        RunCase(nameof(SubnetSharingScopeTenant), "scenario-subnet-sharing-rg", run =>
        {
            CliGroupCreate(run, location);
            Cli(run, "vnet", "create", "--resource-group", run.ResourceGroup, "--name", "vnet1", "--location", location,
                "--address-prefixes", "10.0.0.0/16");

            SubnetCli(run, "create", "vnet1", "--name", "subnet1", "--address-prefixes", "10.0.0.0/16",
                "--default-outbound", "false", "--sharing-scope", "Tenant");
            Assert.Equal("Tenant", Single(SubnetCli(run, "show", "vnet1", "--name", "subnet1")).SharingScope);

            CliGroupDelete(run);
        });
    }

    /// <summary>
    /// Record: runs the CLI steps once against live Azure with a run-scoped group name, then saves.
    /// Playback: replays the recording through the CLI steps.
    /// </summary>
    private static void RunCase(string testName, string resourceGroup, Action<Run> cli)
    {
        var recording = SessionRecordFiles.OpenSession(typeof(SubnetScenarioTests), testName);

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(resourceGroup, recording.Mode);
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: s_extensions);
            try
            {
                cli(new Run(host, groupName));
                recording.Save();
            }
            finally
            {
                // After the save, so this safety delete never lands in the recording.
                try { host.RunCli("group", "delete", "--name", groupName); } catch { }
            }
            return;
        }

        using var playbackHost = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions);
        cli(new Run(playbackHost, resourceGroup));
    }

    private static IReadOnlyList<object> Cli(Run run, params string[] args) => run.Host.RunCli(["network", .. args]);

    private static IReadOnlyList<object> SubnetCli(Run run, string verb, string vnetName, params string[] args) =>
        Cli(run, ["vnet", "subnet", verb, "--resource-group", run.ResourceGroup, "--vnet-name", vnetName, .. args]);

    private static void CliGroupCreate(Run run, string location) =>
        run.Host.RunCli("group", "create", "--name", run.ResourceGroup, "--location", location);

    private static void CliGroupDelete(Run run) => run.Host.RunCli("group", "delete", "--name", run.ResourceGroup);

    private static Subnet Single(IReadOnlyList<object> outputs) => Assert.IsType<Subnet>(Assert.Single(outputs));

    /// <summary>List output arrives either item by item or as one page collection; flatten both.</summary>
    private static List<Subnet> Items(IReadOnlyList<object> outputs) =>
        [.. outputs.SelectMany(output => output switch
        {
            Subnet subnet => [subnet],
            IEnumerable<Subnet> page => page,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected list output {output.GetType()}."),
        })];
}
