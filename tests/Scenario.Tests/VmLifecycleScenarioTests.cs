using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The virtual machine lifecycle: create, list, show, stop, deallocate, start, delete, driven through the
/// real dispatcher/binder and ARM request pipeline. Record mode runs the CLI surface against live
/// Azure and rewrites the recording; playback replays the committed recording on both surfaces
/// without credentials.
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
    public void CreateListShowStopDeallocateStartDeleteVirtualMachine()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(VmLifecycleScenarioTests), nameof(CreateListShowStopDeallocateStartDeleteVirtualMachine));

        if (recording.Mode == RecorderMode.Record)
        {
            // Live ARM, one surface; the subscription comes from the signed-in profile.
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: s_extensions);

            var subscriptionId = host.LiveSubscriptionId();
            RunGroupCreate(host, subscriptionId);
            AssertVm(RunCreate(host, subscriptionId));
            AssertListed(RunList(host, subscriptionId, showDetails: false));
            AssertListedWithDetails(RunList(host, subscriptionId, showDetails: true));
            AssertVm(RunShow(host, subscriptionId));
            AssertActionCompleted(RunStop(host, subscriptionId));
            AssertActionCompleted(RunDeallocate(host, subscriptionId));
            AssertActionCompleted(RunStart(host, subscriptionId));
            AssertActionCompleted(RunDelete(host, subscriptionId));
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunGroupCreate(cli, SubscriptionId);
            AssertVm(RunCreate(cli, SubscriptionId), SubscriptionId);
            AssertListed(RunList(cli, SubscriptionId, showDetails: false), SubscriptionId);
            AssertListedWithDetails(RunList(cli, SubscriptionId, showDetails: true), SubscriptionId);
            AssertVm(RunShow(cli, SubscriptionId), SubscriptionId);
            AssertActionCompleted(RunStop(cli, SubscriptionId));
            AssertActionCompleted(RunDeallocate(cli, SubscriptionId));
            AssertActionCompleted(RunStart(cli, SubscriptionId));
            AssertActionCompleted(RunDelete(cli, SubscriptionId));
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(SubscriptionId), extension: "Resources");
            AssertVm(ps.RunPowerShell("New-AzVM", PsCreateParameters(SubscriptionId), extension: "Compute"), SubscriptionId);
            AssertListed(ps.RunPowerShell("Get-AzVM_List", PsListParameters(SubscriptionId, status: false), extension: "Compute"), SubscriptionId);

            // List-mode -UserData isn't covered: it needs a GET with $expand=userData, which only a
            // PowerShell call makes. Both surfaces replay this one CLI-recorded session, so nothing
            // records that request and its playback would fail.
            AssertListedWithStatus(ps.RunPowerShell("Get-AzVM_List", PsListParameters(SubscriptionId, status: true), extension: "Compute"), SubscriptionId);
            AssertVm(ps.RunPowerShell("Get-AzVM_Get", PsVmParameters(SubscriptionId), extension: "Compute"), SubscriptionId);

            // Stop-AzVM spans both REST operations the CLI splits across two commands, so these two
            // invocations replay the requests `vm stop` and `vm deallocate` recorded, in that order:
            // -StayProvisioned selects powerOff, and the default branch deallocates.
            AssertActionCompleted(ps.RunPowerShell("Stop-AzVM", PsStopParameters(SubscriptionId, stayProvisioned: true), extension: "Compute"));
            AssertActionCompleted(ps.RunPowerShell("Stop-AzVM", PsStopParameters(SubscriptionId, stayProvisioned: false), extension: "Compute"));

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

    /// <summary>
    /// <c>vm list</c> scoped to the scenario's resource group. <paramref name="showDetails"/> passes the
    /// short alias <c>-d</c>, so the step also exercises the alias wiring.
    /// </summary>
    private static IReadOnlyList<object> RunList(ScenarioHost host, string subscriptionId, bool showDetails)
    {
        var args = new List<string>
        {
            "vm", "list",
            "--subscription-id", subscriptionId,
            "--resource-group", ResourceGroupName,
        };
        if (showDetails)
        {
            args.Add("-d");
        }
        return host.RunCli(args.ToArray());
    }

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

    private static IReadOnlyList<object> RunStop(ScenarioHost host, string subscriptionId) =>
        host.RunCli("vm", "stop",
            "--subscription-id", subscriptionId,
            "--name", VmName,
            "--resource-group", ResourceGroupName);

    private static IReadOnlyList<object> RunDeallocate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("vm", "deallocate",
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

    /// <summary>
    /// <c>Get-AzVM_List</c> scoped to the scenario's resource group; <paramref name="status"/> adds
    /// <c>-Status</c>.
    /// </summary>
    private static Dictionary<string, object> PsListParameters(string subscriptionId, bool status)
    {
        var parameters = new Dictionary<string, object>
        {
            ["SubscriptionId"] = subscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
        };
        if (status)
        {
            parameters["Status"] = true;
        }
        return parameters;
    }

    /// <summary>
    /// Stop-AzVM addressing plus the switch that selects the REST operation: <c>-StayProvisioned</c>
    /// powers the VM off, and its absence deallocates.
    /// </summary>
    private static Dictionary<string, object> PsStopParameters(string subscriptionId, bool stayProvisioned)
    {
        var parameters = PsVmParameters(subscriptionId);
        if (stayProvisioned)
        {
            parameters["StayProvisioned"] = true;
        }
        return parameters;
    }

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
    /// Every list step writes exactly one object, the <c>List&lt;VirtualMachine&gt;</c> the list
    /// orchestrator builds, holding the scenario's single VM. <c>Assert.IsType</c> is an exact match,
    /// so an array, another collection, or VMs written one at a time all fail. Returns the VM so the
    /// detail checks can build on it.
    /// </summary>
    private static VirtualMachine AssertListed(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var vms = Assert.IsType<List<VirtualMachine>>(Assert.Single(outputs));
        AssertVm(vms, expectedSubscriptionId);
        return vms[0];
    }

    /// <summary>
    /// <c>vm list -d</c>: the power state comes from the instance view, and the network fields from
    /// the NIC lookup. <c>vm create</c> allocates no public IP, so <c>PublicIps</c> and <c>Fqdns</c>
    /// are empty strings, not null; null would mean the lookup never ran.
    /// </summary>
    private static void AssertListedWithDetails(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var vm = AssertListed(outputs, expectedSubscriptionId);
        Assert.Equal("VM running", vm.PowerState);
        Assert.StartsWith("10.0.0.", vm.PrivateIps);
        Assert.False(string.IsNullOrEmpty(vm.MacAddresses));
        Assert.Equal("", vm.PublicIps);
        Assert.Equal("", vm.Fqdns);
        Assert.Null(vm.Properties.InstanceView);

        // Regression guard: can't fail with today's wiring. The CLI has no -Status, so vm list -d passes
        // includeStatusFields: false and must never surface the -Status-only fields.
        Assert.Null(vm.OsName);
        Assert.Null(vm.OsVersion);
        Assert.Null(vm.HyperVGeneration);
        Assert.Null(vm.MaintenanceRedeployStatus);
    }

    /// <summary>
    /// <c>Get-AzVM -Status</c>: the power state and the -Status-only fields come from the instance view.
    /// Of those fields only <c>HyperVGeneration</c> is asserted: the platform reports it, while the
    /// guest agent reports <c>OsName</c> and <c>OsVersion</c> and can lag on a freshly created VM.
    /// </summary>
    private static void AssertListedWithStatus(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var vm = AssertListed(outputs, expectedSubscriptionId);
        Assert.Equal("VM running", vm.PowerState);
        Assert.False(string.IsNullOrEmpty(vm.HyperVGeneration));
        Assert.Null(vm.Properties.InstanceView);

        // Regression guard: can't fail with today's wiring. PowerShell has no -ShowDetails, so -Status
        // must never run the NIC lookup that fills the network fields.
        Assert.Null(vm.PublicIps);
        Assert.Null(vm.Fqdns);
        Assert.Null(vm.PrivateIps);
        Assert.Null(vm.MacAddresses);
    }

    /// <summary>
    /// stop, deallocate, start, and delete are body-less actions. The long-running operation's final
    /// GET can surface an operation-status payload, so the assertion is that the command completed
    /// and returned no virtual machine model rather than an exact output count.
    /// </summary>
    private static void AssertActionCompleted(IReadOnlyList<object> outputs) =>
        Assert.DoesNotContain(outputs, output => output is VirtualMachine);
}
