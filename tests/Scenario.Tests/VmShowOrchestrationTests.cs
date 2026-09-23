// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Drives `vm show` and `Get-AzVM` through the real dispatcher, binder and engine against a fake
/// ARM, so the orchestrator's expand resolution, id fan-out and detail enrichment are asserted
/// without a recording or credentials.
/// </summary>
[Collection("Scenario Tests")]
public class VmShowOrchestrationTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-show-rg";
    private const string VmName = "scenario-show-vm";

    private static string VmId(string name) =>
        $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{name}";

    /// <summary>
    /// Boots a host wired to a fresh fake ARM. The recorder is required by the constructor, but the
    /// assertions read the backend's captured requests, which are not sanitised.
    /// </summary>
    private static ScenarioHost NewHost(ClientType clientType, out FakeVmShowArmBackend backend)
    {
        backend = new FakeVmShowArmBackend(SubscriptionId);
        return new ScenarioHost(
            clientType,
            new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()),
            backend,
            extensions: [ExtensionUnderTest.Compute]);
    }

    [Fact]
    public void PlainShow_SendsNoExpand_AndReturnsTheVirtualMachine()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var outputs = host.RunCli("vm", "show",
            "--subscription-id", SubscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", VmName);

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Null(vm.Properties.UserData);
        Assert.DoesNotContain(backend.Requests, uri => uri.Contains("$expand", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IncludeUserData_ExpandsUserData_AndSurfacesIt()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var outputs = host.RunCli("vm", "show",
            "--subscription-id", SubscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", VmName,
            "--include-user-data");

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(FakeVmShowArmBackend.UserDataValue, vm.Properties.UserData);
        Assert.Contains(backend.Requests, uri => uri.Contains("$expand=userData", StringComparison.Ordinal));
    }

    [Fact]
    public void IncludeUserData_ShortAlias_BindsTheSameWay()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        host.RunCli("vm", "show",
            "--subscription-id", SubscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", VmName,
            "-u");

        Assert.Contains(backend.Requests, uri => uri.Contains("$expand=userData", StringComparison.Ordinal));
    }

    [Fact]
    public void GetAzVM_UserData_ExpandsUserData()
    {
        using var host = NewHost(ClientType.PowerShell, out var backend);

        var outputs = host.RunPowerShell("Get-AzVM_Get", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = VmName,
            ["UserData"] = true,
        }, extension: "Compute");

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(FakeVmShowArmBackend.UserDataValue, vm.Properties.UserData);
        Assert.Contains(backend.Requests, uri => uri.Contains("$expand=userData", StringComparison.Ordinal));
    }

    [Fact]
    public void Ids_FetchEachVirtualMachine_AndWriteAnArray()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var outputs = host.RunCli("vm", "show", "--ids", VmId("vm-one"), VmId("vm-two"));

        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        Assert.Equal(new[] { "vm-one", "vm-two" }, vms.Select(vm => vm.Name).ToArray());
        Assert.Equal(2, backend.Requests.Count);
    }

    [Fact]
    public void SingleId_StillWritesAnArray()
    {
        using var host = NewHost(ClientType.CLI, out _);

        var outputs = host.RunCli("vm", "show", "--ids", VmId(VmName));

        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        Assert.Equal(VmName, Assert.Single(vms).Name);
    }

    [Fact]
    public void Ids_CarryTheExpandToEveryFetch()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        host.RunCli("vm", "show", "--ids", VmId("vm-one"), VmId("vm-two"), "--include-user-data");

        Assert.Equal(2, backend.Requests.Count);
        Assert.All(backend.Requests, uri => Assert.True(uri.Contains("$expand=userData", StringComparison.Ordinal), uri));
    }

    [Fact]
    public void Ids_WinOverNameAndResourceGroup()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var outputs = host.RunCli("vm", "show",
            "--resource-group", "ignored-rg",
            "--name", "ignored-vm",
            "--ids", VmId("vm-one"));

        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        Assert.Equal("vm-one", Assert.Single(vms).Name);
        Assert.All(backend.Requests, uri => Assert.False(uri.Contains("ignored-vm", StringComparison.Ordinal), uri));
    }

    [Fact]
    public void NoIdsAndNoName_FailsBeforeAnyRequest()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            host.RunCli("vm", "show", "--subscription-id", SubscriptionId));

        Assert.Contains("resource id", exception.Message);
        Assert.Empty(backend.Requests);
    }

    [Fact]
    public void MalformedId_IsRejectedBeforeAnyRequest()
    {
        using var host = NewHost(ClientType.CLI, out var backend);

        Assert.Throws<InvalidOperationException>(() =>
            host.RunCli("vm", "show", "--ids", "/subscriptions/abc/not-a-virtual-machine"));

        Assert.Empty(backend.Requests);
    }

    [Fact]
    public void GetAzVM_Id_ResolvesThroughTheSharedResolver()
    {
        using var host = NewHost(ClientType.PowerShell, out var backend);

        var outputs = host.RunPowerShell("Get-AzVM_Id", new Dictionary<string, object>
        {
            ["Id"] = new[] { VmId(VmName) },
        }, extension: "Compute");

        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        Assert.Equal(VmName, Assert.Single(vms).Name);
        Assert.Single(backend.Requests);
    }
}
