using System.Diagnostics;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Recording;

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

    // A public IP the fake is seeded with, in the VM's resource group.
    private const string SharedPublicIpId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-vm-rg/providers/Microsoft.Network/publicIPAddresses/shared-ip";

    // A well-formed id the fake has never heard of.
    private const string MissingPublicIpId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/shared-rg/providers/Microsoft.Network/publicIPAddresses/missing-ip";

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
        "--size", "Standard_D2s_v3",
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
    public void Size_BindsVmSize()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli(s_cliArgs));
        }

        var vmBody = record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/virtualMachines/")).Request.Body!;
        Assert.Contains("\"vmSize\":\"Standard_D2s_v3\"", vmBody);
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

    [Fact]
    public void AcceleratedNetworking_IsOffByDefault_AndOptIn()
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli(s_cliArgs));
        }
        Assert.Contains("\"enableAcceleratedNetworking\":false", NicBody(record));

        record = new SessionRecord();
        recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.CLI, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli([.. s_cliArgs, "--accelerated-networking", "true"]));
        }
        Assert.Contains("\"enableAcceleratedNetworking\":true", NicBody(record));

        record = new SessionRecord();
        recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using (var host = new ScenarioHost(ClientType.PowerShell, recorder, new FakeVmArmBackend(SubscriptionId), extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunPowerShell("New-AzResourceGroup", PsGroupParameters(), extension: "Resources");
            var parameters = PsParameters();
            parameters["AcceleratedNetworking"] = true;
            AssertVm(host.RunPowerShell("New-AzVM", parameters, extension: "Compute"));
        }
        Assert.Contains("\"enableAcceleratedNetworking\":true", NicBody(record));

        static string NicBody(SessionRecord record)
            => record.Entries.First(e => e.Request.Method == "PUT" && e.Request.Uri.Contains("/networkInterfaces/")).Request.Body!;
    }

    [Fact]
    public void PublicIp_Omitted_CreatesTheDefaultWithoutALookup()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), s_cliArgs);

        var publicIpPutIndex = record.Entries.FindIndex(e => IsRequest(e, "PUT", "/publicIPAddresses/scenario-vmPublicIP?"));
        Assert.True(publicIpPutIndex >= 0);
        Assert.DoesNotContain(record.Entries[..publicIpPutIndex], e => IsRequest(e, "GET", "/publicIPAddresses/"));
        Assert.Contains(record.Entries, e => IsRequest(e, "PUT", "/publicIPAddresses/scenario-vmPublicIP?"));
        Assert.Contains("/publicIPAddresses/scenario-vmPublicIP", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void PublicIp_EmptyString_AttachesNone()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgs, "--public-ip-address", ""]);

        Assert.DoesNotContain(record.Entries, e => e.Request.Uri.Contains("/publicIPAddresses/"));
        Assert.DoesNotContain("publicIPAddress", PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void PublicIp_ExistingName_IsAttachedWithoutBeingPut()
    {
        var record = RecordCli(new FakeVmArmBackend(SubscriptionId, SharedPublicIpId), [.. s_cliArgs, "--public-ip-address", "shared-ip"]);

        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/publicIPAddresses/shared-ip?"));
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/publicIPAddresses/"));
        Assert.Contains(SharedPublicIpId, PutBody(record, "/networkInterfaces/"));
    }

    [Fact]
    public void PublicIp_MissingResourceId_FailsBeforeAnythingIsCreated()
    {
        var (record, error) = RecordCliFailure(new FakeVmArmBackend(SubscriptionId), [.. s_cliArgs, "--public-ip-address", MissingPublicIpId]);

        Assert.Equal($"Public IP address '{MissingPublicIpId}' was not found.", error.Message);
        Assert.Contains(record.Entries, e => IsRequest(e, "GET", "/resourceGroups/shared-rg/providers/Microsoft.Network/publicIPAddresses/missing-ip?"));

        // Every network and VM request carries /providers/; the resource group create does not.
        Assert.DoesNotContain(record.Entries, e => IsRequest(e, "PUT", "/providers/"));
    }

    private static void AssertVm(IReadOnlyList<object> outputs)
    {
        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Equal("westus2", vm.Location);
        Assert.Equal("Succeeded", vm.Properties.ProvisioningState);
    }

    /// <summary>Runs group create then vm create against <paramref name="backend"/>, recording both.</summary>
    private static SessionRecord RecordCli(FakeVmArmBackend backend, string[] vmCreateArgs)
    {
        var record = new SessionRecord();
        using (var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record), backend, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]))
        {
            host.RunCli(s_groupArgs);
            AssertVm(host.RunCli(vmCreateArgs));
        }
        return record;
    }

    /// <summary>Like <see cref="RecordCli"/>, for a vm create that must fail; returns what it sent and the error.</summary>
    private static (SessionRecord Record, InvalidOperationException Error) RecordCliFailure(FakeVmArmBackend backend, string[] vmCreateArgs)
    {
        var record = new SessionRecord();
        using var host = new ScenarioHost(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record), backend, extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Compute]);
        host.RunCli(s_groupArgs);
        var error = Assert.Throws<InvalidOperationException>(() => host.RunCli(vmCreateArgs));
        return (record, error);
    }

    private static bool IsRequest(SessionRecordEntry entry, string method, string uriFragment)
        => entry.Request.Method == method && entry.Request.Uri.Contains(uriFragment);

    /// <summary>The body of the first PUT whose URI contains <paramref name="uriFragment"/>.</summary>
    private static string PutBody(SessionRecord record, string uriFragment)
        => record.Entries.First(e => IsRequest(e, "PUT", uriFragment)).Request.Body!;
}
