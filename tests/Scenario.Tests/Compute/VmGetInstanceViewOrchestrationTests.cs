using System.Management.Automation;
using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Compute;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Drives <c>vm get-instance-view</c>, and <c>Get-AzVM</c> through the real cmdlet, through the real
/// dispatcher, binder and ARM request pipeline against a fake ARM. That pins the orchestration without a
/// live recording: the expand it sends, the shape it writes, where it fails, and which PowerShell parameter
/// set reaches it.
/// </summary>
[Collection("Scenario Tests")]
public class VmGetInstanceViewOrchestrationTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-vm-rg";
    private const string VmName = "scenario-vm";
    private const string SecondVmName = "scenario-vm-2";
    private const string UserDataValue = "dXNlci1kYXRh";

    [Fact]
    public void ByNameAndResourceGroup_WritesTheVmWithItsInstanceViewIntact()
    {
        var (outputs, record) = Run("vm", "get-instance-view", "--subscription-id", SubscriptionId, "-g", ResourceGroupName, "-n", VmName);

        AssertInstanceView(Assert.IsType<VirtualMachine>(Assert.Single(outputs)), VmName);
        var entry = Assert.Single(record.Entries);
        Assert.Equal("GET", entry.Request.Method);
        Assert.EndsWith(
            $"/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{VmName}?api-version=2025-11-01&$expand=instanceView",
            entry.Request.Uri);
    }

    [Fact]
    public void Ids_WritesOneVmPerIdInInputOrder()
    {
        var (outputs, record) = Run("vm", "get-instance-view", "--ids", VmId(VmName), VmId(SecondVmName));

        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        Assert.Equal(new[] { VmName, SecondVmName }, vms.Select(vm => vm.Name));
        Assert.All(vms, vm => AssertInstanceView(vm, vm.Name));
        Assert.Equal(2, record.Entries.Count);
        Assert.All(record.Entries, entry => Assert.EndsWith("?api-version=2025-11-01&$expand=instanceView", entry.Request.Uri));
    }

    [Fact]
    public void SingleId_WritesAOneElementArray()
    {
        var (outputs, record) = Run("vm", "get-instance-view", "--ids", VmId(VmName));

        // --ids always writes an array, even for one id.
        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        AssertInstanceView(Assert.Single(vms), VmName);
        var entry = Assert.Single(record.Entries);
        Assert.Equal("GET", entry.Request.Method);
        Assert.EndsWith(
            $"/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{VmName}?api-version=2025-11-01&$expand=instanceView",
            entry.Request.Uri);
    }

    [Fact]
    public void MissingResourceGroup_FailsBeforeAnyRequest()
    {
        var record = new SessionRecord();
        using var host = Host(record);

        var error = Assert.Throws<InvalidInputException>(
            () => host.RunCli("vm", "get-instance-view", "--subscription-id", SubscriptionId, "-n", VmName));

        Assert.Equal("Specify the virtual machine by both name and resource group, or address it by resource id.", error.Message);
        Assert.Empty(record.Entries);
    }

    [Fact]
    public void MalformedId_FailsBeforeAnyRequest()
    {
        var record = new SessionRecord();
        using var host = Host(record);
        var nicId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces/scenario-vm-nic";

        var error = Assert.Throws<InvalidInputException>(
            () => host.RunCli("vm", "get-instance-view", "--ids", VmId(VmName), nicId));

        Assert.StartsWith($"Invalid virtual machine resource id '{nicId}'.", error.Message);
        Assert.Empty(record.Entries);
    }

    [Fact]
    public void UnknownVm_FailsWithArmNotFound()
    {
        var record = new SessionRecord();
        using var host = Host(record);

        var error = Assert.Throws<HttpRequestException>(
            () => host.RunCli("vm", "get-instance-view", "--subscription-id", SubscriptionId, "-g", ResourceGroupName, "-n", "missing-vm"));

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
    }

    [Fact]
    public void PowerShellStatus_ByNameAndResourceGroup_WritesTheVmWithItsInstanceViewIntact()
    {
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = VmName,
            ["Status"] = true,
        });

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        AssertInstanceView(vm, VmName);
        Assert.Null(vm.Properties.UserData);
        Assert.EndsWith(
            $"/virtualMachines/{VmName}?api-version=2025-11-01&$expand=instanceView",
            Assert.Single(record.Entries).Request.Uri);
    }

    [Fact]
    public void PowerShellStatusWithUserData_ExpandsBothInOneRequest()
    {
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = VmName,
            ["Status"] = true,
            ["UserData"] = true,
        });

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        AssertInstanceView(vm, VmName);
        Assert.Equal(UserDataValue, vm.Properties.UserData);
        Assert.EndsWith(
            "?api-version=2025-11-01&$expand=instanceView,userData",
            Uri.UnescapeDataString(Assert.Single(record.Entries).Request.Uri));
    }

    [Fact]
    public void PowerShellStatus_ById_WritesTheVmWithItsInstanceView()
    {
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["Id"] = VmId(VmName),
            ["Status"] = true,
        });

        // The cmdlet enumerates the orchestrator's array, so PowerShell sees one VM per id.
        AssertInstanceView(Assert.IsType<VirtualMachine>(Assert.Single(outputs)), VmName);
        Assert.EndsWith(
            $"/virtualMachines/{VmName}?api-version=2025-11-01&$expand=instanceView",
            Assert.Single(record.Entries).Request.Uri);
    }

    [Fact]
    public void PowerShellIdStatusWithUserData_ExpandsBothInOneRequest()
    {
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["Id"] = VmId(VmName),
            ["Status"] = true,
            ["UserData"] = true,
        });

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        AssertInstanceView(vm, VmName);
        Assert.Equal(UserDataValue, vm.Properties.UserData);
        Assert.EndsWith(
            $"/virtualMachines/{VmName}?api-version=2025-11-01&$expand=instanceView,userData",
            Uri.UnescapeDataString(Assert.Single(record.Entries).Request.Uri));
    }

    [Fact]
    public void PowerShellWithoutStatus_ReadsThePlainVm()
    {
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = VmName,
        });

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Null(vm.Properties.InstanceView);
        Assert.EndsWith($"/virtualMachines/{VmName}?api-version=2025-11-01", Assert.Single(record.Entries).Request.Uri);
    }

    [Fact]
    public void PowerShellId_ReadsThePlainVm()
    {
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["Id"] = VmId(VmName),
        });

        AssertPlainVm(outputs, record);
    }

    [Fact]
    public void PowerShellPositionalId_ReadsThePlainVm()
    {
        // Get-AzVM <id>: Id and IdStatus both take position 0, and only Id binds without -Status.
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>(), arguments: [VmId(VmName)]);

        AssertPlainVm(outputs, record);
    }

    [Fact]
    public void PowerShellPipedId_ReadsThePlainVm()
    {
        // [pscustomobject]@{ Id = <id> } | Get-AzVM: Id and IdStatus both take Id by property name.
        var piped = new PSObject();
        piped.Properties.Add(new PSNoteProperty("Id", VmId(VmName)));

        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>(), pipelineInput: [piped]);

        AssertPlainVm(outputs, record);
    }

    [Fact]
    public void PowerShellStatusFalse_ByNameAndResourceGroup_ReadsThePlainVm()
    {
        // -Status:$false still binds GetStatus, but legacy treats a false switch as absent.
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = VmName,
            ["Status"] = false,
        });

        AssertPlainVm(outputs, record);
    }

    [Fact]
    public void PowerShellStatusFalse_ById_ReadsThePlainVm()
    {
        // -Status:$false still binds IdStatus, but legacy treats a false switch as absent.
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["Id"] = VmId(VmName),
            ["Status"] = false,
        });

        AssertPlainVm(outputs, record);
    }

    [Fact]
    public void PowerShellListStatus_StillFlattensThePowerState()
    {
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Status"] = true,
        });

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal("VM running", vm.PowerState);
        Assert.Equal("V2", vm.HyperVGeneration);
        Assert.Null(vm.Properties.InstanceView);
        Assert.Equal(2, record.Entries.Count);
        Assert.EndsWith(
            $"/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines?api-version=2025-11-01",
            record.Entries[0].Request.Uri);
        Assert.EndsWith($"/virtualMachines/{VmName}?api-version=2025-11-01&$expand=instanceView", record.Entries[1].Request.Uri);
    }

    [Fact]
    public void PowerShellNameStatus_WithoutResourceGroup_ListsThenFlattensThePowerState()
    {
        // -Name -Status without a resource group binds ListByName, not GetStatus.
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = VmName,
            ["Status"] = true,
        });

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Equal("VM running", vm.PowerState);
        Assert.Null(vm.Properties.InstanceView);
        Assert.Equal(2, record.Entries.Count);
        Assert.EndsWith(
            $"/subscriptions/{SubscriptionId}/providers/Microsoft.Compute/virtualMachines?api-version=2025-11-01",
            record.Entries[0].Request.Uri);
        Assert.EndsWith($"/virtualMachines/{VmName}?api-version=2025-11-01&$expand=instanceView", record.Entries[1].Request.Uri);
    }

    /// <summary>
    /// The fake's instance view arrived whole, and get-instance-view set none of the fields the
    /// detail switches flatten onto the VM.
    /// </summary>
    private static void AssertInstanceView(VirtualMachine vm, string expectedName)
    {
        Assert.Equal(expectedName, vm.Name);
        Assert.Equal(VmId(expectedName), vm.Id);

        var view = Assert.IsType<VirtualMachineInstanceView>(vm.Properties.InstanceView);
        Assert.Equal(new[] { "ProvisioningState/succeeded", "PowerState/running" }, view.Statuses.Select(status => status.Code));
        Assert.Equal("Info", view.Statuses[0].Level);
        Assert.Equal("2026-11-05T00:00:00+00:00", view.Statuses[0].Time);
        Assert.Equal("V2", view.HyperVGeneration);
        Assert.NotNull(view.VmAgent);
        Assert.Equal("2.7.41491.1102", view.VmAgent.VmAgentVersion);
        Assert.NotNull(view.Disks);
        Assert.Equal($"{expectedName}_OsDisk", Assert.Single(view.Disks).Name);

        Assert.Null(vm.PowerState);
        Assert.Null(vm.PublicIps);
        Assert.Null(vm.Fqdns);
        Assert.Null(vm.PrivateIps);
        Assert.Null(vm.MacAddresses);
        Assert.Null(vm.OsName);
        Assert.Null(vm.OsVersion);
        Assert.Null(vm.HyperVGeneration);
        Assert.Null(vm.MaintenanceRedeployStatus);
    }

    /// <summary>
    /// PowerShell wrote one VM without an instance view, read by exactly one GET with no <c>$expand</c>:
    /// the plain <c>Get-AzVM_Get</c> or <c>Get-AzVM_Id</c> read.
    /// </summary>
    private static void AssertPlainVm(IReadOnlyList<object> outputs, SessionRecord record)
    {
        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Null(vm.Properties.InstanceView);

        var uri = Assert.Single(record.Entries).Request.Uri;
        Assert.EndsWith($"/virtualMachines/{VmName}?api-version=2025-11-01", uri);
        Assert.DoesNotContain("$expand", uri);
    }

    private static (IReadOnlyList<object> Outputs, SessionRecord Record) Run(params string[] args)
    {
        var record = new SessionRecord();
        using var host = Host(record);
        return (host.RunCli(args), record);
    }

    private static ScenarioHost Host(SessionRecord record) =>
        new(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record), new InstanceViewArm(), extensions: [ExtensionUnderTest.Compute]);

    /// <summary>
    /// Runs the real <c>Get-AzVM</c> cmdlet, so PowerShell's own parameter binding picks the parameter
    /// set, and through it the command key. <paramref name="arguments"/> bind by position, and
    /// <paramref name="pipelineInput"/> is piped in.
    /// </summary>
    private static (IReadOnlyList<object> Outputs, SessionRecord Record) RunGetAzVM(
        Dictionary<string, object> parameters, IEnumerable<object>? arguments = null, IEnumerable<object>? pipelineInput = null)
    {
        var record = new SessionRecord();
        using var host = new ScenarioHost(ClientType.PowerShell, new RecordPlaybackInterceptor(RecorderMode.Record, record), new InstanceViewArm(), extensions: [ExtensionUnderTest.Compute]);
        return (host.RunPowerShellCmdlet<GetVMCmdlet>(parameters, arguments, pipelineInput), record);
    }

    private static string VmId(string name) =>
        $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{name}";

    /// <summary>
    /// Stands in for ARM. The resource group's VM list holds the first VM. A GET of either known VM answers
    /// with the VM, carrying its instance view and user data only when the request expands them, as ARM
    /// does. Any other VM answers 404.
    /// </summary>
    private sealed class InstanceViewArm : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.TrimEnd('/');
            var query = Uri.UnescapeDataString(request.RequestUri.Query);
            if (request.Method != HttpMethod.Get
                || !path.Contains("/providers/Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Fake ARM got an unexpected request: {request.Method} {request.RequestUri}");
            }

            if (path.EndsWith("/providers/Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json(new { value = new[] { Vm(VmName, query) } }, HttpStatusCode.OK));
            }

            var name = path.Split('/')[^1];
            if (name != VmName && name != SecondVmName)
            {
                return Task.FromResult(Json(new
                {
                    error = new
                    {
                        code = "ResourceNotFound",
                        message = $"The Resource 'Microsoft.Compute/virtualMachines/{name}' under resource group '{ResourceGroupName}' was not found.",
                    },
                }, HttpStatusCode.NotFound));
            }

            return Task.FromResult(Json(Vm(name, query), HttpStatusCode.OK));
        }

        private static object Vm(string name, string query) => new
        {
            id = VmId(name),
            name,
            location = "westus2",
            properties = new
            {
                provisioningState = "Succeeded",
                hardwareProfile = new { vmSize = "Standard_D2s_v3" },
                userData = query.Contains("userData", StringComparison.OrdinalIgnoreCase) ? UserDataValue : null,
                instanceView = query.Contains("$expand=instanceView", StringComparison.OrdinalIgnoreCase) ? InstanceView(name) : null,
            },
        };

        private static object InstanceView(string name) => new
        {
            computerName = name,
            hyperVGeneration = "V2",
            vmAgent = new
            {
                vmAgentVersion = "2.7.41491.1102",
                statuses = new[] { new { code = "ProvisioningState/succeeded", level = "Info", displayStatus = "Ready" } },
            },
            disks = new[]
            {
                new
                {
                    name = $"{name}_OsDisk",
                    statuses = new[] { new { code = "ProvisioningState/succeeded", level = "Info", displayStatus = "Provisioning succeeded" } },
                },
            },
            statuses = new object[]
            {
                new { code = "ProvisioningState/succeeded", level = "Info", displayStatus = "Provisioning succeeded", time = "2026-11-05T00:00:00+00:00" },
                new { code = "PowerState/running", level = "Info", displayStatus = "VM running" },
            },
        };

        private static HttpResponseMessage Json(object body, HttpStatusCode status) => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
    }
}
