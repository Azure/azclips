using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using NetworkPublicIpAddress = Microsoft.Azure.Azclips.Extensions.Network.Models.PublicIPAddress;
using NetworkVirtualNetwork = Microsoft.Azure.Azclips.Extensions.Network.Models.VirtualNetwork;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The virtual machine lifecycle: create, list, show, stop, deallocate, start, delete, driven through
/// the real dispatcher/binder and ARM request pipeline. Two virtual machines are created so both
/// delete shapes are covered: a plain delete, and one with <c>--force-deletion</c>/<c>-ForceDeletion</c>,
/// which adds the <c>forceDeletion</c> query parameter. Every create passes tags. After the first one,
/// the Network extension reads back the virtual network and default public IP that create made, and
/// they must carry the tags too. Record mode runs the CLI surface against live Azure and rewrites the
/// recording; playback replays the committed recording on both surfaces without credentials.
/// After show, stop, deallocate and start, <c>vm get-instance-view</c> on the CLI, and <c>Get-AzVM -Status</c>
/// in PowerShell, must report the power state each one left behind.
/// </summary>
[Collection("Scenario Tests")]
public class VmLifecycleScenarioTests
{
    // Playback matches the placeholder the sanitisers wrote into the recording.
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-vm-lifecycle-rg";

    // Azure caps the Windows computer name, derived from the VM name, at 15 characters.
    private const string VmName = "lifecycle-vm";
    private const string ForceDeleteVmName = "lifecycle-vm-2";
    private const string Location = "australiaeast";
    private const string Password = "PlaceholderP@ss1";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute, ExtensionUnderTest.Network];

    [Fact]
    public void CreateListShowStopDeallocateStartDeleteVirtualMachine()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(VmLifecycleScenarioTests), nameof(CreateListShowStopDeallocateStartDeleteVirtualMachine));

        if (recording.Mode == RecorderMode.Record)
        {
            // Live ARM, one surface; the subscription comes from the signed-in profile.
            var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: s_extensions);

            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                RunGroupCreate(host, subscriptionId, groupName);
                AssertVm(RunCreate(host, subscriptionId, VmName, "1", groupName), VmName);
                AssertVirtualNetworkTagged(RunVnetShow(host, subscriptionId, "1", groupName));
                AssertPublicIpTagged(RunPublicIpShow(host, subscriptionId, VmName, groupName));
                AssertListed(RunList(host, subscriptionId, showDetails: false, groupName));
                AssertListedWithDetails(RunList(host, subscriptionId, showDetails: true, groupName));
                AssertVm(RunShow(host, subscriptionId, VmName, groupName), VmName);
                AssertInstanceView(RunGetInstanceView(host, subscriptionId, VmName, shortAliases: true, groupName: groupName), "PowerState/running", detailed: true);
                AssertActionCompleted(RunStop(host, subscriptionId, VmName, groupName));
                AssertInstanceView(RunGetInstanceView(host, subscriptionId, VmName, groupName: groupName), "PowerState/stopped");
                AssertActionCompleted(RunDeallocate(host, subscriptionId, VmName, groupName));
                AssertInstanceView(RunGetInstanceViewByIds(host, VmId(subscriptionId, VmName, groupName)), "PowerState/deallocated");
                AssertActionCompleted(RunStart(host, subscriptionId, VmName, groupName));
                AssertInstanceView(RunGetInstanceView(host, subscriptionId, VmName, groupName: groupName), "PowerState/running");
                AssertVm(RunCreate(host, subscriptionId, ForceDeleteVmName, "2", groupName), ForceDeleteVmName);
                AssertActionCompleted(RunDelete(host, subscriptionId, VmName, groupName));
                AssertActionCompleted(RunForceDelete(host, subscriptionId, ForceDeleteVmName, groupName));
                recording.Save();
            }
            finally
            {
                // After the save, so the delete never lands in the recording.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
            }
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunGroupCreate(cli, SubscriptionId);
            AssertVm(RunCreate(cli, SubscriptionId, VmName, "1"), VmName, SubscriptionId);
            AssertVirtualNetworkTagged(RunVnetShow(cli, SubscriptionId, "1"));
            AssertPublicIpTagged(RunPublicIpShow(cli, SubscriptionId, VmName));
            AssertListed(RunList(cli, SubscriptionId, showDetails: false), SubscriptionId);
            AssertListedWithDetails(RunList(cli, SubscriptionId, showDetails: true), SubscriptionId);
            AssertVm(RunShow(cli, SubscriptionId, VmName), VmName, SubscriptionId);
            AssertInstanceView(RunGetInstanceView(cli, SubscriptionId, VmName, shortAliases: true), "PowerState/running", SubscriptionId, detailed: true);
            AssertActionCompleted(RunStop(cli, SubscriptionId, VmName));
            AssertInstanceView(RunGetInstanceView(cli, SubscriptionId, VmName), "PowerState/stopped", SubscriptionId);
            AssertActionCompleted(RunDeallocate(cli, SubscriptionId, VmName));
            AssertInstanceView(RunGetInstanceViewByIds(cli, VmId(SubscriptionId, VmName)), "PowerState/deallocated", SubscriptionId);
            AssertActionCompleted(RunStart(cli, SubscriptionId, VmName));
            AssertInstanceView(RunGetInstanceView(cli, SubscriptionId, VmName), "PowerState/running", SubscriptionId);
            AssertVm(RunCreate(cli, SubscriptionId, ForceDeleteVmName, "2"), ForceDeleteVmName, SubscriptionId);
            AssertActionCompleted(RunDelete(cli, SubscriptionId, VmName));
            AssertActionCompleted(RunForceDelete(cli, SubscriptionId, ForceDeleteVmName));
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            // Get-AzVM_GetStatus and Get-AzVM_IdStatus send the same $expand=instanceView reads the CLI's
            // vm get-instance-view records, in the same order, so they replay those entries. The first is
            // recorded after vm list -d, so New-AzVM and Get-AzVM_List -Status below still consume the
            // $expand=instanceView entries they did before.
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(SubscriptionId), extension: "Resources");
            AssertVm(ps.RunPowerShell("New-AzVM", PsCreateParameters(SubscriptionId, VmName, "1"), extension: "Compute"), VmName, SubscriptionId);
            AssertVirtualNetworkTagged(ps.RunPowerShell("Get-AzVirtualNetwork_Get", PsNetworkParameters(SubscriptionId, "scenario-lifecycle-vnet-1"), extension: "Network"));
            AssertPublicIpTagged(ps.RunPowerShell("Get-AzPublicIpAddress_Get", PsNetworkParameters(SubscriptionId, $"{VmName}PublicIP"), extension: "Network"));
            AssertListed(ps.RunPowerShell("Get-AzVM_List", PsListParameters(SubscriptionId, status: false), extension: "Compute"), SubscriptionId);

            // List-mode -UserData isn't covered: it needs a GET with $expand=userData, which only a
            // PowerShell call makes. Both surfaces replay this one CLI-recorded session, so nothing
            // records that request and its playback would fail.
            AssertListedWithStatus(ps.RunPowerShell("Get-AzVM_List", PsListParameters(SubscriptionId, status: true), extension: "Compute"), SubscriptionId);
            AssertVm(ps.RunPowerShell("Get-AzVM_Get", PsVmParameters(SubscriptionId, VmName), extension: "Compute"), VmName, SubscriptionId);
            AssertInstanceView(ps.RunPowerShell("Get-AzVM_GetStatus", PsVmParameters(SubscriptionId, VmName), extension: "Compute"), "PowerState/running", SubscriptionId, detailed: true);

            // Stop-AzVM spans both REST operations the CLI splits across two commands, so these two
            // invocations replay the requests `vm stop` and `vm deallocate` recorded, in that order:
            // -StayProvisioned selects powerOff, and the default branch deallocates.
            AssertActionCompleted(ps.RunPowerShell("Stop-AzVM", PsStopParameters(SubscriptionId, VmName, stayProvisioned: true), extension: "Compute"));
            AssertInstanceView(ps.RunPowerShell("Get-AzVM_GetStatus", PsVmParameters(SubscriptionId, VmName), extension: "Compute"), "PowerState/stopped", SubscriptionId);
            AssertActionCompleted(ps.RunPowerShell("Stop-AzVM", PsStopParameters(SubscriptionId, VmName, stayProvisioned: false), extension: "Compute"));
            AssertInstanceView(ps.RunPowerShell("Get-AzVM_IdStatus", new Dictionary<string, object> { ["Id"] = new[] { VmId(SubscriptionId, VmName) } }, extension: "Compute"), "PowerState/deallocated", SubscriptionId);

            // -Id exercises the orchestrator's resource-id path; it resolves to the same
            // subscription/resource group/name, so it replays the request the CLI recorded.
            AssertActionCompleted(ps.RunPowerShell("Start-AzVM", new Dictionary<string, object> { ["Id"] = new[] { VmId(SubscriptionId, VmName) } }, extension: "Compute"));
            AssertInstanceView(ps.RunPowerShell("Get-AzVM_GetStatus", PsVmParameters(SubscriptionId, VmName), extension: "Compute"), "PowerState/running", SubscriptionId);

            AssertVm(ps.RunPowerShell("New-AzVM", PsCreateParameters(SubscriptionId, ForceDeleteVmName, "2"), extension: "Compute"), ForceDeleteVmName, SubscriptionId);
            AssertActionCompleted(ps.RunPowerShell("Remove-AzVM", PsVmParameters(SubscriptionId, VmName), extension: "Compute"));

            // -ForceDeletion adds the forceDeletion query parameter, so this replays the
            // force-delete request rather than the plain one above.
            var forceDeleteParameters = PsVmParameters(SubscriptionId, ForceDeleteVmName);
            forceDeleteParameters["ForceDeletion"] = true;
            AssertActionCompleted(ps.RunPowerShell("Remove-AzVM", forceDeleteParameters, extension: "Compute"));
        }
    }

    private static IReadOnlyList<object> RunGroupCreate(ScenarioHost host, string subscriptionId, string? groupName = null) =>
        host.RunCli("group", "create",
            "--subscription-id", subscriptionId,
            "--name", groupName ?? ResourceGroupName,
            "--location", Location);

    private static IReadOnlyList<object> RunCreate(ScenarioHost host, string subscriptionId, string vmName, string suffix, string? groupName = null) =>
        host.RunCli("vm", "create",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--location", Location,
            "--resource-group", groupName ?? ResourceGroupName,
            "--size", "Standard_D2ds_v6",
            "--admin-username", "azadmin",
            "--admin-password", Password,
            "--virtual-network-name", $"scenario-lifecycle-vnet-{suffix}",
            "--subnet-names", "default",
            "--subnet-address-prefixes", "10.0.0.0/24",
            "--address-prefixes", "10.0.0.0/16",
            "--tags", "env=scenario", "suite=vm-lifecycle");

    private static IReadOnlyList<object> RunVnetShow(ScenarioHost host, string subscriptionId, string suffix, string? groupName = null) =>
        host.RunCli("network", "vnet", "show",
            "--subscription-id", subscriptionId,
            "--name", $"scenario-lifecycle-vnet-{suffix}",
            "--resource-group", groupName ?? ResourceGroupName);

    /// <summary><c>network public-ip show</c> for the default public IP <c>vm create</c> made for <paramref name="vmName"/>.</summary>
    private static IReadOnlyList<object> RunPublicIpShow(ScenarioHost host, string subscriptionId, string vmName, string? groupName = null) =>
        host.RunCli("network", "public-ip", "show",
            "--subscription-id", subscriptionId,
            "--name", $"{vmName}PublicIP",
            "--resource-group", groupName ?? ResourceGroupName);

    /// <summary>
    /// <c>vm list</c> scoped to the scenario's resource group. <paramref name="showDetails"/> passes the
    /// short alias <c>-d</c>, so the step also exercises the alias wiring.
    /// </summary>
    private static IReadOnlyList<object> RunList(ScenarioHost host, string subscriptionId, bool showDetails, string? groupName = null)
    {
        var args = new List<string>
        {
            "vm", "list",
            "--subscription-id", subscriptionId,
            "--resource-group", groupName ?? ResourceGroupName,
        };
        if (showDetails)
        {
            args.Add("-d");
        }
        return host.RunCli(args.ToArray());
    }

    private static IReadOnlyList<object> RunShow(ScenarioHost host, string subscriptionId, string vmName, string? groupName = null) =>
        host.RunCli("vm", "show",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--resource-group", groupName ?? ResourceGroupName);

    /// <summary>
    /// <c>vm get-instance-view</c> by name. <paramref name="shortAliases"/> spells the addressing as
    /// <c>-n</c>/<c>-g</c>, so one step also exercises the alias wiring.
    /// </summary>
    private static IReadOnlyList<object> RunGetInstanceView(ScenarioHost host, string subscriptionId, string vmName, bool shortAliases = false, string? groupName = null) =>
        host.RunCli("vm", "get-instance-view",
            "--subscription-id", subscriptionId,
            shortAliases ? "-n" : "--name", vmName,
            shortAliases ? "-g" : "--resource-group", groupName ?? ResourceGroupName);

    /// <summary><c>vm get-instance-view --ids</c>. The subscription comes from the id, so none is passed.</summary>
    private static IReadOnlyList<object> RunGetInstanceViewByIds(ScenarioHost host, string vmId) =>
        host.RunCli("vm", "get-instance-view", "--ids", vmId);

    private static IReadOnlyList<object> RunStart(ScenarioHost host, string subscriptionId, string vmName, string? groupName = null) =>
        host.RunCli("vm", "start",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--resource-group", groupName ?? ResourceGroupName);

    private static IReadOnlyList<object> RunStop(ScenarioHost host, string subscriptionId, string vmName, string? groupName = null) =>
        host.RunCli("vm", "stop",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--resource-group", groupName ?? ResourceGroupName);

    private static IReadOnlyList<object> RunDeallocate(ScenarioHost host, string subscriptionId, string vmName, string? groupName = null) =>
        host.RunCli("vm", "deallocate",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--resource-group", groupName ?? ResourceGroupName);

    private static IReadOnlyList<object> RunDelete(ScenarioHost host, string subscriptionId, string vmName, string? groupName = null) =>
        host.RunCli("vm", "delete",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--resource-group", groupName ?? ResourceGroupName);

    private static IReadOnlyList<object> RunForceDelete(ScenarioHost host, string subscriptionId, string vmName, string? groupName = null) =>
        host.RunCli("vm", "delete",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--resource-group", groupName ?? ResourceGroupName,
            "--force-deletion", "true");

    private static Dictionary<string, object> PsGroupParameters(string subscriptionId) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = ResourceGroupName,
        ["Location"] = Location,
    };

    private static Dictionary<string, object> PsCreateParameters(string subscriptionId, string vmName, string suffix) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = vmName,
        ["Location"] = Location,
        ["ResourceGroupName"] = ResourceGroupName,
        ["VmSize"] = "Standard_D2ds_v6",
        ["AdminUsername"] = "azadmin",
        ["AdminPassword"] = Password,
        ["VirtualNetworkName"] = $"scenario-lifecycle-vnet-{suffix}",
        ["SubnetNames"] = new List<string> { "default" },
        ["SubnetAddressPrefixes"] = new List<List<string>> { new() { "10.0.0.0/24" } },
        ["AddressPrefixes"] = new List<string> { "10.0.0.0/16" },
        // New-AzVM sends its -Tags hashtable as one JSON object string.
        ["Tags"] = new[] { "{\"env\":\"scenario\",\"suite\":\"vm-lifecycle\"}" },
    };

    private static Dictionary<string, object> PsVmParameters(string subscriptionId, string vmName) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = vmName,
        ["ResourceGroupName"] = ResourceGroupName,
    };

    private static Dictionary<string, object> PsNetworkParameters(string subscriptionId, string name) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = name,
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
    private static Dictionary<string, object> PsStopParameters(string subscriptionId, string vmName, bool stayProvisioned)
    {
        var parameters = PsVmParameters(subscriptionId, vmName);
        if (stayProvisioned)
        {
            parameters["StayProvisioned"] = true;
        }
        return parameters;
    }

    private static string VmId(string subscriptionId, string vmName, string? groupName = null) =>
        $"/subscriptions/{subscriptionId}/resourceGroups/{groupName ?? ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}";

    private static void AssertVm(IReadOnlyList<object> outputs, string expectedName, string? expectedSubscriptionId = null)
    {
        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(expectedName, vm.Name);
        Assert.Equal(Location, vm.Location);
        Assert.Equal("Succeeded", vm.Properties.ProvisioningState);
        AssertTagged(vm.Tags);
        if (expectedSubscriptionId is not null)
        {
            Assert.Equal(VmId(expectedSubscriptionId, expectedName), vm.Id);
        }
    }

    /// <summary>
    /// The tags every <c>vm create</c> in this scenario passes. Only these two are checked, not the
    /// count: the live subscription a recording runs in may add tags of its own through Azure Policy.
    /// </summary>
    private static void AssertTagged(IDictionary<string, string>? tags)
    {
        Assert.NotNull(tags);
        Assert.Equal("scenario", tags["env"]);
        Assert.Equal("vm-lifecycle", tags["suite"]);
    }

    private static void AssertVirtualNetworkTagged(IReadOnlyList<object> outputs) =>
        AssertTagged(Assert.IsType<NetworkVirtualNetwork>(Assert.Single(outputs)).Tags);

    private static void AssertPublicIpTagged(IReadOnlyList<object> outputs) =>
        AssertTagged(Assert.IsType<NetworkPublicIpAddress>(Assert.Single(outputs)).Tags);

    /// <summary>
    /// Every list step writes exactly one object, the <c>List&lt;VirtualMachine&gt;</c> the list
    /// orchestrator builds. The list steps run before the force-delete VM is created, so the resource
    /// group holds only <see cref="VmName"/>. <c>Assert.IsType</c> is an exact match, so an array,
    /// another collection, or VMs written one at a time all fail. Returns the VM so the detail checks
    /// can build on it.
    /// </summary>
    private static VirtualMachine AssertListed(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var vms = Assert.IsType<List<VirtualMachine>>(Assert.Single(outputs));
        AssertVm(vms, VmName, expectedSubscriptionId);
        return vms[0];
    }

    /// <summary>
    /// <c>vm list -d</c>: the power state comes from the instance view, and the network fields from
    /// the NIC and public IP lookups. <c>vm create</c> attaches its default Standard public IP, so
    /// <c>PublicIps</c> is populated; it sets no DNS label, so <c>Fqdns</c> is an empty string, not
    /// null — null would mean the lookup never ran.
    /// </summary>
    private static void AssertListedWithDetails(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
    {
        var vm = AssertListed(outputs, expectedSubscriptionId);
        Assert.Equal("VM running", vm.PowerState);
        Assert.StartsWith("10.0.0.", vm.PrivateIps);
        Assert.False(string.IsNullOrEmpty(vm.MacAddresses));
        Assert.False(string.IsNullOrEmpty(vm.PublicIps));
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
    /// <c>vm get-instance-view</c> and <c>Get-AzVM -Status</c>: one virtual machine, or a one-element array
    /// for <c>--ids</c> and <c>-Id</c>, whose instance view holds exactly the provisioning status and the
    /// power state the previous step left.
    /// The flattened detail fields stay null, because get-instance-view returns the instance view in
    /// place. <paramref name="detailed"/> also checks the platform-reported hypervisor generation, the
    /// VM agent, and the single OS disk. Guest-agent values, disk names and timestamps change on every
    /// re-record, so they are never asserted.
    /// </summary>
    private static void AssertInstanceView(IReadOnlyList<object> outputs, string expectedPowerState, string? expectedSubscriptionId = null, bool detailed = false)
    {
        var output = Assert.Single(outputs);
        var vm = output is VirtualMachine[] byIds ? Assert.Single(byIds) : Assert.IsType<VirtualMachine>(output);
        Assert.Equal(VmName, vm.Name);
        if (expectedSubscriptionId is not null)
        {
            Assert.Equal(VmId(expectedSubscriptionId, VmName), vm.Id);
        }

        var view = Assert.IsType<VirtualMachineInstanceView>(vm.Properties.InstanceView);
        Assert.Equal(2, view.Statuses.Count);
        Assert.Equal("ProvisioningState/succeeded", view.Statuses[0].Code);
        Assert.Equal("Info", view.Statuses[0].Level);
        Assert.Equal(expectedPowerState, view.Statuses[1].Code);

        Assert.Null(vm.PowerState);
        Assert.Null(vm.PublicIps);
        Assert.Null(vm.Fqdns);
        Assert.Null(vm.PrivateIps);
        Assert.Null(vm.MacAddresses);
        Assert.Null(vm.OsName);
        Assert.Null(vm.OsVersion);
        Assert.Null(vm.HyperVGeneration);
        Assert.Null(vm.MaintenanceRedeployStatus);

        if (detailed)
        {
            Assert.False(string.IsNullOrEmpty(view.HyperVGeneration));
            Assert.NotNull(view.VmAgent);
            Assert.NotNull(view.Disks);
            var osDisk = Assert.Single(view.Disks);
            Assert.NotNull(osDisk.Statuses);
            Assert.Contains(osDisk.Statuses, status => status.Code == "ProvisioningState/succeeded");
        }
    }

    /// <summary>
    /// stop, deallocate, start, and delete are body-less actions. The long-running operation's final
    /// GET can surface an operation-status payload, so the assertion is that the command completed
    /// and returned no virtual machine model rather than an exact output count.
    /// </summary>
    private static void AssertActionCompleted(IReadOnlyList<object> outputs) =>
        Assert.DoesNotContain(outputs, output => output is VirtualMachine);
}
