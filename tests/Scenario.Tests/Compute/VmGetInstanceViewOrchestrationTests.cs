using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Drives <c>vm get-instance-view</c> through the real dispatcher, binder and ARM request pipeline
/// against a fake ARM, so the orchestration is pinned without a live recording: the expand it
/// sends, the shape it writes, and where it fails.
/// </summary>
[Collection("Scenario Tests")]
public class VmGetInstanceViewOrchestrationTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-vm-rg";
    private const string VmName = "scenario-vm";
    private const string SecondVmName = "scenario-vm-2";

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

    private static (IReadOnlyList<object> Outputs, SessionRecord Record) Run(params string[] args)
    {
        var record = new SessionRecord();
        using var host = Host(record);
        return (host.RunCli(args), record);
    }

    private static ScenarioHost Host(SessionRecord record) =>
        new(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record), new InstanceViewArm(), extensions: [ExtensionUnderTest.Compute]);

    private static string VmId(string name) =>
        $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{name}";

    /// <summary>
    /// Stands in for ARM: a GET of either known VM answers with the VM, carrying its instance view only
    /// when the request asks for <c>$expand=instanceView</c>, as ARM does. Any other VM answers 404.
    /// </summary>
    private sealed class InstanceViewArm : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method != HttpMethod.Get
                || !path.Contains("/providers/Microsoft.Compute/virtualMachines/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Fake ARM got an unexpected request: {request.Method} {request.RequestUri}");
            }

            var name = path.TrimEnd('/').Split('/')[^1];
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

            var expanded = Uri.UnescapeDataString(request.RequestUri.Query).Contains("$expand=instanceView", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(Json(new
            {
                id = VmId(name),
                name,
                location = "westus2",
                properties = new
                {
                    provisioningState = "Succeeded",
                    hardwareProfile = new { vmSize = "Standard_D2s_v3" },
                    instanceView = expanded ? InstanceView(name) : null,
                },
            }, HttpStatusCode.OK));
        }

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
