using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The vm create scenario: the whole orchestration plus its resource group in one recording,
/// replayed credential-free on both surfaces. Record mode runs the CLI surface once against
/// real Azure with the signed-in profile's subscription.
/// <para>
/// Besides the VM, the scenario asserts the network resources vm create depends on: the PUT each
/// surface sends for the virtual network, network security group, public IP and network interface,
/// and how ARM reported each resource once created. Playback matches requests on method and URI
/// only, so the sent bodies are checked from what this run sent, not from the recording.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class VmCreateScenarioTests
{
    // Playback matches the placeholder the sanitisers wrote into the recording.
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-vm-rg";
    private const string VmName = "scenario-vm";
    private const string Location = "westus2";
    private const string Password = "PlaceholderP@ss1";
    private const string Image = "MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition:latest";

    [Fact]
    public void CreatesVirtualMachine()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(VmCreateScenarioTests), nameof(CreatesVirtualMachine));

        if (recording.Mode == RecorderMode.Record)
        {
            // Live ARM, one surface; the subscription comes from the signed-in profile. The group
            // name carries a run-scoped suffix, stripped by the sanitisers before the file is written.
            var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
            var interceptor = recording.CreateInterceptor();
            using var host = new ScenarioHost(ClientType.CLI, interceptor, useLiveAuth: true, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);
            var subscription = host.LiveSubscriptionId();
            try
            {
                host.RunCli(GroupArgs(subscription, groupName));
                var vm = AssertVm(host.RunCli(CliArgs(subscription, groupName)));
                // The sent bodies carry the suffixed group; the saved entries carry the base name.
                AssertSentDependencies(interceptor.Sent, groupName);

                // Checked against the live responses before anything is saved.
                AssertCreatedDependencies(recording.Record.Entries, vm);
                recording.Save();
            }
            finally
            {
                // After the save; deleting the group removes the vm and its network resources.
                try { host.RunCli("group", "delete", "--subscription-id", subscription, "--name", groupName); } catch { }
            }
            return;
        }

        var cliInterceptor = recording.CreateInterceptor();
        using (var cli = new ScenarioHost(ClientType.CLI, cliInterceptor, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            cli.RunCli(GroupArgs(SubscriptionId));
            var vm = AssertVm(cli.RunCli(CliArgs(SubscriptionId)));
            AssertSentDependencies(cliInterceptor.Sent);
            AssertCreatedDependencies(recording.Record.Entries, vm);
        }

        var psInterceptor = recording.CreateInterceptor();
        using (var ps = new ScenarioHost(ClientType.PowerShell, psInterceptor, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(SubscriptionId), extension: "Resources");
            AssertVm(ps.RunPowerShell("New-AzVM", PsParameters(SubscriptionId), extension: "Compute"));
            AssertSentDependencies(psInterceptor.Sent);
        }
    }

    private static string[] GroupArgs(string subscriptionId, string? groupName = null) =>
    [
        "group", "create",
        "--subscription-id", subscriptionId,
        "--name", groupName ?? ResourceGroupName,
        "--location", Location,
    ];

    private static Dictionary<string, object> PsGroupParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = ResourceGroupName,
        ["Location"] = Location,
    };

    private static string[] CliArgs(string subscriptionId, string? groupName = null) =>
    [
        "vm", "create",
        "--subscription-id", subscriptionId,
        "--name", VmName,
        "--location", Location,
        "--resource-group", groupName ?? ResourceGroupName,
        "--size", "Standard_D2ds_v6",
        "--image", Image,
        "--admin-username", "azadmin",
        "--admin-password", Password,
        "--virtual-network-name", "scenario-vm-vnet",
        "--subnet-names", "default",
        "--subnet-address-prefixes", "10.0.0.0/24",
        "--address-prefixes", "10.0.0.0/16",
    ];

    private static Dictionary<string, object> PsParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = VmName,
        ["Location"] = Location,
        ["ResourceGroupName"] = ResourceGroupName,
        ["VmSize"] = "Standard_D2ds_v6",
        ["Image"] = Image,
        ["AdminUsername"] = "azadmin",
        ["AdminPassword"] = Password,
        ["VirtualNetworkName"] = "scenario-vm-vnet",
        ["SubnetNames"] = new List<string> { "default" },
        ["SubnetAddressPrefixes"] = new List<List<string>> { new() { "10.0.0.0/24" } },
        ["AddressPrefixes"] = new List<string> { "10.0.0.0/16" },
    };

    private static VirtualMachine AssertVm(IReadOnlyList<object> outputs)
    {
        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Equal(Location, vm.Location);
        Assert.Equal("Succeeded", vm.Properties.ProvisioningState);

        // vm create writes the az vm show -d view: the default public IP is attached, and no DNS
        // label is set, so Fqdns is an empty string rather than null.
        Assert.Equal("VM running", vm.PowerState);
        Assert.False(string.IsNullOrEmpty(vm.PublicIps));
        Assert.StartsWith("10.0.0.", vm.PrivateIps);
        Assert.False(string.IsNullOrEmpty(vm.MacAddresses));
        Assert.Equal("", vm.Fqdns);
        Assert.Null(vm.Properties.InstanceView);
        return vm;
    }

    /// <summary>What vm create asked ARM for, read from the requests this run sent.</summary>
    private static void AssertSentDependencies(IReadOnlyList<CapturedHttpRequest> sent, string? groupName = null)
    {
        var vnet = RecordedArm.SentPut<VirtualNetwork>(sent, "virtualNetworks", "scenario-vm-vnet");
        Assert.Equal("10.0.0.0/16", Assert.Single(vnet.Properties.AddressSpace.AddressPrefixes));
        Assert.Equal("10.0.0.0/24", Assert.Single(vnet.Properties.Subnets).Properties.AddressPrefix);

        // A new NSG gets legacy CLI's default rule for the Windows image vm create deploys.
        var nsg = RecordedArm.SentPut<NetworkSecurityGroup>(sent, "networkSecurityGroups", $"{VmName}NSG");
        var rule = Assert.Single(nsg.Properties.SecurityRules);
        Assert.Equal("rdp", rule.Name);
        Assert.Equal("3389", rule.Properties.DestinationPortRange);
        Assert.Equal(1000, rule.Properties.Priority);

        var publicIp = RecordedArm.SentPut<PublicIPAddress>(sent, "publicIPAddresses", $"{VmName}PublicIP");
        Assert.Equal("Standard", publicIp.Sku!.Name);
        Assert.Equal("Static", publicIp.Properties.PublicIPAllocationMethod);

        var nic = RecordedArm.SentPut<NetworkInterface>(sent, "networkInterfaces", "scenario-vmVMNic");
        var ipConfig = Assert.Single(nic.Properties.IPConfigurations).Properties;
        Assert.EndsWith(NetworkId("networkSecurityGroups", $"{VmName}NSG", groupName), nic.Properties.NetworkSecurityGroup!.Id, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(NetworkId("publicIPAddresses", $"{VmName}PublicIP", groupName), ipConfig.PublicIPAddress!.Id, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(NetworkId("virtualNetworks", "scenario-vm-vnet", groupName) + "/subnets/default", ipConfig.Subnet.Id, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What ARM created: each resource's GET at the end of its long-running create.</summary>
    private static void AssertCreatedDependencies(IReadOnlyList<SessionRecordEntry> entries, VirtualMachine vm)
    {
        var vnet = RecordedArm.FinalGet<VirtualNetwork>(entries, "virtualNetworks", "scenario-vm-vnet");
        Assert.Equal("10.0.0.0/24", Assert.Single(vnet.Properties.Subnets).Properties.AddressPrefix);

        var nsg = RecordedArm.FinalGet<NetworkSecurityGroup>(entries, "networkSecurityGroups", $"{VmName}NSG");
        Assert.Equal("rdp", Assert.Single(nsg.Properties.SecurityRules).Name);

        var publicIp = RecordedArm.FinalGet<PublicIPAddress>(entries, "publicIPAddresses", $"{VmName}PublicIP");
        Assert.Equal("Static", publicIp.Properties.PublicIPAllocationMethod);
        Assert.Equal(vm.PublicIps, publicIp.Properties.IpAddress);

        var nic = RecordedArm.FinalGet<NetworkInterface>(entries, "networkInterfaces", "scenario-vmVMNic");
        Assert.EndsWith(NetworkId("networkSecurityGroups", $"{VmName}NSG"), nic.Properties.NetworkSecurityGroup!.Id, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(NetworkId("publicIPAddresses", $"{VmName}PublicIP"), Assert.Single(nic.Properties.IPConfigurations).Properties.PublicIPAddress!.Id, StringComparison.OrdinalIgnoreCase);
    }

    // Compared by suffix: when recording, the sent bodies carry the live subscription, not the
    // placeholder, and the run-scoped group name rather than the base one.
    private static string NetworkId(string type, string name, string? groupName = null)
        => $"/resourceGroups/{groupName ?? ResourceGroupName}/providers/Microsoft.Network/{type}/{name}";
}
