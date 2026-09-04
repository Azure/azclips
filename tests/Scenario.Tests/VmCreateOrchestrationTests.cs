using System.Diagnostics;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Core.Recording;

namespace Scenario.Tests;

/// <summary>
/// Proves the vm create orchestration records and replays against a fake ARM: chained LRO
/// creates, vnet and nsg in parallel, downstream requests built from upstream response ids.
/// </summary>
[Collection("Scenario Tests")]
public class VmCreateOrchestrationTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string VmName = "scenario-vm";
    private const string Password = "PlaceholderP@ss1";

    private static readonly string[] s_groupArgs =
    [
        "group", "create",
        "--subscription-id", SubscriptionId,
        "--name", "scenario-vm-rg",
        "--location", "westus2",
    ];

    private static Dictionary<string, object> PsGroupParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["Name"] = "scenario-vm-rg",
        ["Location"] = "westus2",
    };

    private static readonly string[] s_cliArgs =
    [
        "vm", "create",
        "--subscription-id", SubscriptionId,
        "--name", VmName,
        "--location", "westus2",
        "--resource-group", "scenario-vm-rg",
        "--vm-size", "Standard_D2s_v3",
        "--admin-username", "azadmin",
        "--admin-password", Password,
        "--network-interface-name", "scenario-vm-nic",
        "--virtual-network-name", "scenario-vm-vnet",
        "--network-security-group-name", "scenario-vm-nsg",
        "--subnet-names", "default",
        "--subnet-address-prefixes", "10.0.0.0/24",
        "--address-prefixes", "10.0.0.0/16",
    ];

    private static Dictionary<string, object> PsParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["Name"] = VmName,
        ["Location"] = "westus2",
        ["ResourceGroupName"] = "scenario-vm-rg",
        ["VmSize"] = "Standard_D2s_v3",
        ["AdminUsername"] = "azadmin",
        ["AdminPassword"] = Password,
        ["NetworkInterfaceName"] = "scenario-vm-nic",
        ["VirtualNetworkName"] = "scenario-vm-vnet",
        ["NetworkSecurityGroupName"] = "scenario-vm-nsg",
        ["SubnetNames"] = new List<string> { "default" },
        ["SubnetAddressPrefixes"] = new List<List<string>> { new() { "10.0.0.0/24" } },
        ["AddressPrefixes"] = new List<string> { "10.0.0.0/16" },
    };

    private static SessionRecord RecordFromCli()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);
        host.RunCli(s_groupArgs);
        AssertVm(host.RunCli(s_cliArgs));
        return record;
    }

    [Fact]
    public void Record_CapturesTheWholeChain_Sanitised()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli(s_cliArgs));
        }

        var uris = record.Entries.Select(e => e.Request.Uri).ToList();
        Assert.Contains(uris, u => u.Contains("/virtualNetworks/scenario-vm-vnet"));
        Assert.Contains(uris, u => u.Contains("/networkSecurityGroups/scenario-vm-nsg"));
        Assert.Contains(uris, u => u.Contains("/networkInterfaces/scenario-vm-nic"));
        Assert.Contains(uris, u => u.Contains("/virtualMachines/scenario-vm"));
        Assert.Contains(uris, u => u.Contains("/operations/vm-create"));

        // Downstream requests must embed ids from upstream responses.
        var nicBody = record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/")).Request.Body!;
        Assert.Contains("/subnets/default", nicBody);
        Assert.Contains("/networkSecurityGroups/scenario-vm-nsg", nicBody);
        var vmBody = record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/")).Request.Body!;
        Assert.Contains("/networkInterfaces/scenario-vm-nic", vmBody);

        // Sanitised: the password really went over the wire, but never into the record.
        Assert.Contains(recorder.Sent, r => r.Body is not null && r.Body.Contains(Password));
        Assert.DoesNotContain(Password, vmBody);
        Assert.Contains("[redacted]", vmBody);
    }

    [Fact]
    public void Playback_ReplaysBothSurfaces_FromOneRecording_Fast()
    {
        var record = RecordFromCli();

        // Playback stamps Retry-After to zero, so both replays must beat one recorded interval.
        var clock = Stopwatch.StartNew();

        using (var cli = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Playback, record), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            cli.RunCli(s_groupArgs);
            AssertVm(cli.RunCli(s_cliArgs));
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, new RecordPlaybackInterceptor(RecorderMode.Playback, record), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(), extension: "Resources");
            AssertVm(ps.RunPowerShell("New-AzVM", PsParameters(), extension: "Compute"));
        }

        clock.Stop();
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5),
            $"Playback took {clock.Elapsed}; recorded Retry-After values are being honored instead of neutralized.");
    }

    [Fact]
    public void Playback_ServesConcurrentVnetAndNsg_RegardlessOfRecordedOrder()
    {
        // vnet and nsg run in parallel, so their recorded order is racy. Prove the swap replays.
        var record = RecordFromCli();

        int vnetIndex = record.Entries.FindIndex(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualNetworks/"));
        int nsgIndex = record.Entries.FindIndex(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkSecurityGroups/"));
        (record.Entries[vnetIndex], record.Entries[nsgIndex]) = (record.Entries[nsgIndex], record.Entries[vnetIndex]);

        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Playback, record), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);
        host.RunCli(s_groupArgs);
        AssertVm(host.RunCli(s_cliArgs));
    }

    private static void AssertVm(IReadOnlyList<object> outputs)
    {
        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Equal("westus2", vm.Location);
        Assert.Equal("Succeeded", vm.Properties.ProvisioningState);
    }
}
