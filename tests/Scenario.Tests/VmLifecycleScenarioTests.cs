using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The virtual machine lifecycle: create, list, show, stop, deallocate, start, delete, driven through
/// the real dispatcher/binder and ARM request pipeline. Two virtual machines are created so both
/// delete shapes are covered: a plain delete, and one with <c>--force-deletion</c>/<c>-ForceDeletion</c>,
/// which adds the <c>forceDeletion</c> query parameter. Record mode runs the CLI surface against
/// live Azure and rewrites the recording; playback replays the committed recording on both surfaces
/// without credentials.
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
            AssertVm(RunCreate(host, subscriptionId, VmName, "1"), VmName);
            AssertListed(RunList(host, subscriptionId, showDetails: false));
            AssertListedWithDetails(RunList(host, subscriptionId, showDetails: true));
            AssertVm(RunShow(host, subscriptionId, VmName), VmName);
            AssertActionCompleted(RunStop(host, subscriptionId, VmName));
            AssertActionCompleted(RunDeallocate(host, subscriptionId, VmName));
            AssertActionCompleted(RunStart(host, subscriptionId, VmName));
            AssertVm(RunCreate(host, subscriptionId, ForceDeleteVmName, "2"), ForceDeleteVmName);
            AssertActionCompleted(RunDelete(host, subscriptionId, VmName));
            AssertActionCompleted(RunForceDelete(host, subscriptionId, ForceDeleteVmName));
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunGroupCreate(cli, SubscriptionId);
            AssertVm(RunCreate(cli, SubscriptionId, VmName, "1"), VmName, SubscriptionId);
            AssertListed(RunList(cli, SubscriptionId, showDetails: false), SubscriptionId);
            AssertListedWithDetails(RunList(cli, SubscriptionId, showDetails: true), SubscriptionId);
            AssertVm(RunShow(cli, SubscriptionId, VmName), VmName, SubscriptionId);
            AssertActionCompleted(RunStop(cli, SubscriptionId, VmName));
            AssertActionCompleted(RunDeallocate(cli, SubscriptionId, VmName));
            AssertActionCompleted(RunStart(cli, SubscriptionId, VmName));
            AssertVm(RunCreate(cli, SubscriptionId, ForceDeleteVmName, "2"), ForceDeleteVmName, SubscriptionId);
            AssertActionCompleted(RunDelete(cli, SubscriptionId, VmName));
            AssertActionCompleted(RunForceDelete(cli, SubscriptionId, ForceDeleteVmName));
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(SubscriptionId), extension: "Resources");
            AssertVm(ps.RunPowerShell("New-AzVM", PsCreateParameters(SubscriptionId, VmName, "1"), extension: "Compute"), VmName, SubscriptionId);
            AssertListed(ps.RunPowerShell("Get-AzVM_List", PsListParameters(SubscriptionId, status: false), extension: "Compute"), SubscriptionId);

            // List-mode -UserData isn't covered: it needs a GET with $expand=userData, which only a
            // PowerShell call makes. Both surfaces replay this one CLI-recorded session, so nothing
            // records that request and its playback would fail.
            AssertListedWithStatus(ps.RunPowerShell("Get-AzVM_List", PsListParameters(SubscriptionId, status: true), extension: "Compute"), SubscriptionId);
            AssertVm(ps.RunPowerShell("Get-AzVM_Get", PsVmParameters(SubscriptionId, VmName), extension: "Compute"), VmName, SubscriptionId);

            // Stop-AzVM spans both REST operations the CLI splits across two commands, so these two
            // invocations replay the requests `vm stop` and `vm deallocate` recorded, in that order:
            // -StayProvisioned selects powerOff, and the default branch deallocates.
            AssertActionCompleted(ps.RunPowerShell("Stop-AzVM", PsStopParameters(SubscriptionId, VmName, stayProvisioned: true), extension: "Compute"));
            AssertActionCompleted(ps.RunPowerShell("Stop-AzVM", PsStopParameters(SubscriptionId, VmName, stayProvisioned: false), extension: "Compute"));

            // -Id exercises the orchestrator's resource-id path; it resolves to the same
            // subscription/resource group/name, so it replays the request the CLI recorded.
            AssertActionCompleted(ps.RunPowerShell("Start-AzVM", new Dictionary<string, object> { ["Id"] = new[] { VmId(SubscriptionId, VmName) } }, extension: "Compute"));

            AssertVm(ps.RunPowerShell("New-AzVM", PsCreateParameters(SubscriptionId, ForceDeleteVmName, "2"), extension: "Compute"), ForceDeleteVmName, SubscriptionId);
            AssertActionCompleted(ps.RunPowerShell("Remove-AzVM", PsVmParameters(SubscriptionId, VmName), extension: "Compute"));

            // -ForceDeletion adds the forceDeletion query parameter, so this replays the
            // force-delete request rather than the plain one above.
            var forceDeleteParameters = PsVmParameters(SubscriptionId, ForceDeleteVmName);
            forceDeleteParameters["ForceDeletion"] = true;
            AssertActionCompleted(ps.RunPowerShell("Remove-AzVM", forceDeleteParameters, extension: "Compute"));
        }
    }

    private static IReadOnlyList<object> RunGroupCreate(ScenarioHost host, string subscriptionId) =>
        host.RunCli("group", "create",
            "--subscription-id", subscriptionId,
            "--name", ResourceGroupName,
            "--location", Location);

    private static IReadOnlyList<object> RunCreate(ScenarioHost host, string subscriptionId, string vmName, string suffix) =>
        host.RunCli("vm", "create",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--location", Location,
            "--resource-group", ResourceGroupName,
            "--size", "Standard_D2s_v3",
            "--admin-username", "azadmin",
            "--admin-password", Password,
            "--network-interface-name", $"scenario-lifecycle-nic-{suffix}",
            "--virtual-network-name", $"scenario-lifecycle-vnet-{suffix}",
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

    private static IReadOnlyList<object> RunShow(ScenarioHost host, string subscriptionId, string vmName) =>
        host.RunCli("vm", "show",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--resource-group", ResourceGroupName);

    private static IReadOnlyList<object> RunStart(ScenarioHost host, string subscriptionId, string vmName) =>
        host.RunCli("vm", "start",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--resource-group", ResourceGroupName);

    private static IReadOnlyList<object> RunStop(ScenarioHost host, string subscriptionId, string vmName) =>
        host.RunCli("vm", "stop",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--resource-group", ResourceGroupName);

    private static IReadOnlyList<object> RunDeallocate(ScenarioHost host, string subscriptionId, string vmName) =>
        host.RunCli("vm", "deallocate",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--resource-group", ResourceGroupName);

    private static IReadOnlyList<object> RunDelete(ScenarioHost host, string subscriptionId, string vmName) =>
        host.RunCli("vm", "delete",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--resource-group", ResourceGroupName);

    private static IReadOnlyList<object> RunForceDelete(ScenarioHost host, string subscriptionId, string vmName) =>
        host.RunCli("vm", "delete",
            "--subscription-id", subscriptionId,
            "--name", vmName,
            "--resource-group", ResourceGroupName,
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
        ["VmSize"] = "Standard_D2s_v3",
        ["AdminUsername"] = "azadmin",
        ["AdminPassword"] = Password,
        ["NetworkInterfaceName"] = $"scenario-lifecycle-nic-{suffix}",
        ["VirtualNetworkName"] = $"scenario-lifecycle-vnet-{suffix}",
        ["SubnetNames"] = new List<string> { "default" },
        ["SubnetAddressPrefixes"] = new List<List<string>> { new() { "10.0.0.0/24" } },
        ["AddressPrefixes"] = new List<string> { "10.0.0.0/16" },
    };

    private static Dictionary<string, object> PsVmParameters(string subscriptionId, string vmName) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = vmName,
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

    private static string VmId(string subscriptionId, string vmName) =>
        $"/subscriptions/{subscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}";

    private static void AssertVm(IReadOnlyList<object> outputs, string expectedName, string? expectedSubscriptionId = null)
    {
        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(expectedName, vm.Name);
        Assert.Equal(Location, vm.Location);
        Assert.Equal("Succeeded", vm.Properties.ProvisioningState);
        if (expectedSubscriptionId is not null)
        {
            Assert.Equal(VmId(expectedSubscriptionId, expectedName), vm.Id);
        }
    }

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
    /// stop, deallocate, start, and delete are body-less actions. The long-running operation's final
    /// GET can surface an operation-status payload, so the assertion is that the command completed
    /// and returned no virtual machine model rather than an exact output count.
    /// </summary>
    private static void AssertActionCompleted(IReadOnlyList<object> outputs) =>
        Assert.DoesNotContain(outputs, output => output is VirtualMachine);
}