using Core.Abstractions;
using Core.Recording;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Common.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class VmLifecycleScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-vm-rg";
    private const string VmName = "scenario-vm";
    private const string Location = "westus2";
    private const string Password = "PlaceholderP@ss1";

    [Fact]
    public void CreateShowStartDeleteVm()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(VmLifecycleScenarioTests), nameof(CreateShowStartDeleteVm));

        if (recording.Mode == RecorderMode.Record)
        {
            var interceptor = recording.CreateInterceptor();
            using var host = new ScenarioHost(
                ClientType.CLI,
                interceptor,
                useLiveAuth: true,
                extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);

            var subscriptionId = host.LiveSubscriptionId();
            RunGroupCreate(host, subscriptionId);
            AssertVm(RunCreate(host, subscriptionId));
            AssertVm(RunShow(host, subscriptionId));
            Assert.Empty(RunStart(host, subscriptionId));
            Assert.Empty(RunDelete(host, subscriptionId));
            AssertLifecycleRequests(interceptor, subscriptionId);
            recording.Save();
            return;
        }

        var playback = recording.CreateInterceptor();
        using (var cli = new ScenarioHost(
            ClientType.CLI,
            playback,
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            RunGroupCreate(cli, SubscriptionId);
            AssertVm(RunCreate(cli, SubscriptionId), SubscriptionId);
            AssertVm(RunShow(cli, SubscriptionId), SubscriptionId);
            Assert.Empty(RunStart(cli, SubscriptionId));
            Assert.Empty(RunDelete(cli, SubscriptionId));
        }

        AssertLifecycleRequests(playback, SubscriptionId);
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
            "--network-interface-name", "scenario-vm-nic",
            "--virtual-network-name", "scenario-vm-vnet",
            "--network-security-group-name", "scenario-vm-nsg",
            "--subnet-names", "default",
            "--subnet-address-prefixes", "10.0.0.0/24",
            "--address-prefixes", "10.0.0.0/16");

    private static IReadOnlyList<object> RunShow(ScenarioHost host, string subscriptionId) =>
        host.RunCli("vm", "show",
            "--subscription-id", subscriptionId,
            "--name", VmName,
            "--resource-group", ResourceGroupName);

    // Driven by --ids so the scenario covers resource-id resolution as a user would use it.
    private static IReadOnlyList<object> RunStart(ScenarioHost host, string subscriptionId) =>
        host.RunCli("vm", "start",
            "--ids", VmId(subscriptionId));

    private static IReadOnlyList<object> RunDelete(ScenarioHost host, string subscriptionId) =>
        host.RunCli("vm", "delete",
            "--subscription-id", subscriptionId,
            "--name", VmName,
            "--resource-group", ResourceGroupName);

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
            Assert.Equal(VmId(expectedSubscriptionId), vm.Id, ignoreCase: true);
        }
    }

    /// <summary>
    /// Pins the two steps that return nothing: the start POST and the delete must both have reached
    /// the wire, against the expected vm, and in that order.
    /// </summary>
    private static void AssertLifecycleRequests(RecordPlaybackInterceptor interceptor, string subscriptionId)
    {
        var sent = interceptor.Sent;
        string vmUri = $"https://management.azure.com{VmId(subscriptionId)}";

        int start = IndexOfRequest(sent, HttpMethod.Post.Method, $"{vmUri}/start?");
        int delete = IndexOfRequest(sent, HttpMethod.Delete.Method, $"{vmUri}?");

        Assert.True(start >= 0, $"expected a POST to {vmUri}/start; sent:{Environment.NewLine}{Describe(sent)}");
        Assert.True(delete >= 0, $"expected a DELETE of {vmUri}; sent:{Environment.NewLine}{Describe(sent)}");
        Assert.True(start < delete, $"the vm must be started before it is deleted; sent:{Environment.NewLine}{Describe(sent)}");
    }

    private static int IndexOfRequest(IReadOnlyList<CapturedHttpRequest> sent, string method, string uriPrefix)
    {
        for (int i = 0; i < sent.Count; i++)
        {
            if (string.Equals(sent[i].Method, method, StringComparison.OrdinalIgnoreCase)
                && sent[i].Uri.StartsWith(uriPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    private static string Describe(IReadOnlyList<CapturedHttpRequest> sent) =>
        string.Join(Environment.NewLine, sent.Select(request => $"  {request.Method} {request.Uri}"));
}
