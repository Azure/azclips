using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The vm create scenario: the whole orchestration plus its resource group in one recording,
/// replayed credential-free on both surfaces. Record mode runs the CLI surface once against
/// real Azure with the signed-in profile's subscription.
/// </summary>
[Collection("Scenario Tests")]
public class VmCreateScenarioTests
{
    // Playback matches the placeholder the sanitisers wrote into the recording.
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
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
            // Live ARM, one surface; the subscription comes from the signed-in profile.
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);
            var subscription = host.LiveSubscriptionId();
            host.RunCli(GroupArgs(subscription));
            AssertVm(host.RunCli(CliArgs(subscription)));
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            cli.RunCli(GroupArgs(SubscriptionId));
            AssertVm(cli.RunCli(CliArgs(SubscriptionId)));
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(SubscriptionId), extension: "Resources");
            AssertVm(ps.RunPowerShell("New-AzVM", PsParameters(SubscriptionId), extension: "Compute"));
        }
    }

    private static string[] GroupArgs(string subscriptionId) =>
    [
        "group", "create",
        "--subscription-id", subscriptionId,
        "--name", "scenario-vm-rg",
        "--location", Location,
    ];

    private static Dictionary<string, object> PsGroupParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = "scenario-vm-rg",
        ["Location"] = Location,
    };

    private static string[] CliArgs(string subscriptionId) =>
    [
        "vm", "create",
        "--subscription-id", subscriptionId,
        "--name", VmName,
        "--location", Location,
        "--resource-group", "scenario-vm-rg",
        "--size", "Standard_D2s_v3",
        "--image", Image,
        "--admin-username", "azadmin",
        "--admin-password", Password,
        "--network-interface-name", "scenario-vm-nic",
        "--virtual-network-name", "scenario-vm-vnet",
        "--network-security-group-name", "scenario-vm-nsg",
        "--subnet-names", "default",
        "--subnet-address-prefixes", "10.0.0.0/24",
        "--address-prefixes", "10.0.0.0/16",
    ];

    private static Dictionary<string, object> PsParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = VmName,
        ["Location"] = Location,
        ["ResourceGroupName"] = "scenario-vm-rg",
        ["VmSize"] = "Standard_D2s_v3",
        ["Image"] = Image,
        ["AdminUsername"] = "azadmin",
        ["AdminPassword"] = Password,
        ["NetworkInterfaceName"] = "scenario-vm-nic",
        ["VirtualNetworkName"] = "scenario-vm-vnet",
        ["NetworkSecurityGroupName"] = "scenario-vm-nsg",
        ["SubnetNames"] = new List<string> { "default" },
        ["SubnetAddressPrefixes"] = new List<List<string>> { new() { "10.0.0.0/24" } },
        ["AddressPrefixes"] = new List<string> { "10.0.0.0/16" },
    };

    private static void AssertVm(IReadOnlyList<object> outputs)
    {
        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Equal(Location, vm.Location);
        Assert.Equal("Succeeded", vm.Properties.ProvisioningState);
    }
}
