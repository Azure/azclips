using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Core.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The virtual machine lifecycle: create, show, start, delete, driven through the real
/// dispatcher/binder and ARM request pipeline. Record mode runs the CLI surface against live Azure
/// and rewrites the recording; playback replays the committed recording on both surfaces without
/// credentials.
/// </summary>
[Collection("Scenario Tests")]
public class VmLifecycleScenarioTests
{
    // Playback matches the placeholder the sanitisers wrote into the recording.
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-vm-lifecycle-rg";

    private const string VmName = "lifecycle-vm";
    private const string Location = "westus2";
    private const string Password = "PlaceholderP@ss1";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute];

    [Fact]
    public void CreateShowStartDeleteVirtualMachine()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(VmLifecycleScenarioTests), nameof(CreateShowStartDeleteVirtualMachine));

        if (recording.Mode == RecorderMode.Record)
        {
            // Live ARM, one surface; the subscription comes from the signed-in profile.
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: s_extensions);

            var subscriptionId = host.LiveSubscriptionId();
            RunGroupCreate(host, subscriptionId);
            AssertVm(RunCreate(host, subscriptionId));
            AssertVm(RunShow(host, subscriptionId));
            AssertActionCompleted(RunStart(host, subscriptionId));
            AssertActionCompleted(RunDelete(host, subscriptionId));
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunGroupCreate(cli, SubscriptionId);
            AssertVm(RunCreate(cli, SubscriptionId), SubscriptionId);
            AssertVm(RunShow(cli, SubscriptionId), SubscriptionId);
            AssertActionCompleted(RunStart(cli, SubscriptionId));
            AssertActionCompleted(RunDelete(cli, SubscriptionId));
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(SubscriptionId), extension: "Resources");
            AssertVm(ps.RunPowerShell("New-AzVM", PsCreateParameters(SubscriptionId), extension: "Compute"), SubscriptionId);
            AssertVm(ps.RunPowerShell("Get-AzVM_Get", PsVmParameters(SubscriptionId), extension: "Compute"), SubscriptionId);

            // -Id exercises the orchestrator's resource-id path; it resolves to the same
            // subscription/resource group/name, so it replays the request the CLI recorded.
            AssertActionCompleted(ps.RunPowerShell("Start-AzVM", new Dictionary<string, object> { ["Id"] = new[] { VmId(SubscriptionId) } }, extension: "Compute"));
            AssertActionCompleted(ps.RunPowerShell("Remove-AzVM", PsVmParameters(SubscriptionId), extension: "Compute"));
        }
    }

    private static IReadOnlyList<object> RunGroupCreate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("group", "create",
            "--subscription-id", subscriptionId,
            "--name", ResourceGroupName,
            "--location", Location);

    private static IReadOnlyList<object> RunCreate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("vm", "create",
            "--subscription-id", subscriptionId,
            "--name", VmName,
            "--location", Location,
            "--resource-group", ResourceGroupName,
            "--vm-size", "Standard_D2s_v3",
            "--admin-username", "azadmin",
            "--admin-password", Password,
            "--network-interface-name", "scenario-lifecycle-nic",
            "--virtual-network-name", "scenario-lifecycle-vnet",
            "--network-security-group-name", "scenario-lifecycle-nsg",
            "--subnet-names", "default",
            "--subnet-address-prefixes", "10.0.0.0/24",
            "--address-prefixes", "10.0.0.0/16");

    private static IReadOnlyList<object> RunShow(ScenarioHost host, string subscriptionId) =>
        host.RunCli("vm", "show",
            "--subscription-id", subscriptionId,
            "--name", VmName,
            "--resource-group", ResourceGroupName);

    private static IReadOnlyList<object> RunStart(ScenarioHost host, string subscriptionId) =>
        host.RunCli("vm", "start",
            "--subscription-id", subscriptionId,
            "--name", VmName,
            "--resource-group", ResourceGroupName);

    private static IReadOnlyList<object> RunDelete(ScenarioHost host, string subscriptionId) =>
        host.RunCli("vm", "delete",
            "--subscription-id", subscriptionId,
            "--name", VmName,
            "--resource-group", ResourceGroupName);

    private static Dictionary<string, object> PsGroupParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = ResourceGroupName,
        ["Location"] = Location,
    };

    private static Dictionary<string, object> PsCreateParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = VmName,
        ["Location"] = Location,
        ["ResourceGroupName"] = ResourceGroupName,
        ["VmSize"] = "Standard_D2s_v3",
        ["AdminUsername"] = "azadmin",
        ["AdminPassword"] = Password,
        ["NetworkInterfaceName"] = "scenario-lifecycle-nic",
        ["VirtualNetworkName"] = "scenario-lifecycle-vnet",
        ["NetworkSecurityGroupName"] = "scenario-lifecycle-nsg",
        ["SubnetNames"] = new List<string> { "default" },
        ["SubnetAddressPrefixes"] = new List<List<string>> { new() { "10.0.0.0/24" } },
        ["AddressPrefixes"] = new List<string> { "10.0.0.0/16" },
    };

    private static Dictionary<string, object> PsVmParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = VmName,
        ["ResourceGroupName"] = ResourceGroupName,
    };

    private static string VmId(string subscriptionId) =>
        $"/subscriptions/{subscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{VmName}";

    private static void AssertVm(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Equal(Location, vm.Location);
        Assert.Equal("Succeeded", vm.Properties.ProvisioningState);
        if (expectedSubscriptionId is not null)
        {
            Assert.Equal(VmId(expectedSubscriptionId), vm.Id);
        }
    }

    /// <summary>
    /// start and delete are body-less actions. The long-running operation's final GET can surface an
    /// operation-status payload, so the assertion is that the command completed and returned no
    /// virtual machine model rather than an exact output count.
    /// </summary>
    private static void AssertActionCompleted(IReadOnlyList<object> outputs) =>
        Assert.DoesNotContain(outputs, output => output is VirtualMachine);
}
