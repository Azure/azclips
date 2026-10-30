using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Core.Abstractions;
using Tests.Recording;
using Microsoft.Azure.Azclips.Cmdlets.Network;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Scenario.Tests;

/// <summary>
/// Exercises the network interface lifecycle through the real dispatcher/binder and ARM request
/// pipeline on both client surfaces, recorded in memory against
/// <see cref="FakeNetworkInterfaceArmBackend"/> and replayed immediately, exactly like
/// <see cref="PublicIpScenarioTests"/>.
/// <para>
/// The PowerShell replay matches request bodies byte for byte. The CLI passes the subnet, public IP
/// and network security group as bare names while PowerShell passes full resource ids, so besides
/// the PS/CLI metadata parity the replay also proves the CLI name-to-id resolution produces exactly
/// the ids a caller would otherwise type. The show runs with <c>$expand</c> on both surfaces, so the
/// URI match guards the <c>ExpandResource</c> mapping too.
/// </para>
/// <para>
/// <c>ScenarioHost.RunPowerShell</c> drives the dispatcher with a parameter dictionary and so
/// bypasses the cmdlet classes under <c>src/PS/Network/Cmdlets</c>; those run for real in a
/// runspace in <see cref="PowerShellCmdlets_ConvertTheirParameters_AndRouteParameterSets"/>.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class NetworkInterfaceScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-nic-rg";
    private const string NicName = "scenario-nic";
    private const string Location = "westus2";
    private const string Expand = "ipConfigurations/publicIPAddress";

    private const string ProviderPrefix = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network";
    private const string SubnetId = $"{ProviderPrefix}/virtualNetworks/scenario-vnet/subnets/default";
    private const string PublicIpId = $"{ProviderPrefix}/publicIPAddresses/scenario-pip";
    private const string NsgId = $"{ProviderPrefix}/networkSecurityGroups/scenario-nsg";

    [Fact]
    public void CreateShowListDelete_ReplaysOnBothSurfaces_FromOneRecording()
    {
        var record = RecordFromCli();

        using (var cli = new ScenarioHost(
            ClientType.CLI,
            new RecordPlaybackInterceptor(RecorderMode.Playback, record),
            extensions: [ExtensionUnderTest.Network]))
        {
            AssertCreated(cli.RunCli(CreateArgs()));
            AssertShown(cli.RunCli(ShowArgs()));
            AssertListed(cli.RunCli(ListArgs()));
            Assert.Empty(cli.RunCli(DeleteArgs()));
        }

        using (var ps = new ScenarioHost(
            ClientType.PowerShell,
            new RecordPlaybackInterceptor(RecorderMode.Playback, record, new DefaultRequestMatcher(matchBody: true)),
            extensions: [ExtensionUnderTest.Network]))
        {
            AssertCreated(ps.RunPowerShell("New-AzNetworkInterface", PsCreateParameters(), extension: "Network"));
            AssertShown(ps.RunPowerShell("Get-AzNetworkInterface_Get", PsShowParameters(), extension: "Network"));
            AssertListed(ps.RunPowerShell("Get-AzNetworkInterface_List", PsListParameters(), extension: "Network"));
            Assert.Empty(ps.RunPowerShell("Remove-AzNetworkInterface", PsDeleteParameters(), extension: "Network"));
        }
    }

    /// <summary>
    /// The create must PUT the legacy shape: bare names resolved into ids, the single `ipconfig1`
    /// configuration pinned to the supplied private IP, and the LRO really is a 202 plus a poll.
    /// </summary>
    [Fact]
    public void Create_PutsTheLegacyBody_AndPollsTheAsyncOperation()
    {
        var record = RecordFromCli();
        var create = record.Entries.First(e => e.Request.Method == "PUT");

        Assert.Contains($"/networkInterfaces/{NicName}", create.Request.Uri);
        Assert.Contains("api-version=2025-07-01", create.Request.Uri);

        var body = create.Request.Body!;
        Assert.Contains($"\"subnet\":{{\"id\":\"{SubnetId}\"}}", body);
        Assert.Contains($"\"publicIPAddress\":{{\"id\":\"{PublicIpId}\"}}", body);
        Assert.Contains($"\"networkSecurityGroup\":{{\"id\":\"{NsgId}\"}}", body);
        Assert.Contains("\"name\":\"ipconfig1\"", body);
        Assert.Contains("\"privateIPAddress\":\"10.0.0.10\"", body);
        Assert.Contains("\"privateIPAllocationMethod\":\"Static\"", body);
        Assert.Contains("\"enableAcceleratedNetworking\":true", body);
        Assert.Contains("\"dnsServers\":[\"10.0.0.4\",\"10.0.0.5\"]", body);
        Assert.Contains("\"env\":\"scenario\"", body);

        // Nothing the caller did not ask for may reach the wire.
        Assert.DoesNotContain("enableIPForwarding", body);
        Assert.DoesNotContain("extendedLocation", body);
        Assert.DoesNotContain("privateIPAddressVersion", body);
        Assert.DoesNotContain("primary", body);

        Assert.Contains(record.Entries, e => e.Request.Uri.Contains("/operations/nic-create"));
    }

    /// <summary>
    /// Like the network RP, the delete polls the Azure-AsyncOperation status and finishes on the
    /// Location result, an empty 204, so a finished delete prints nothing (asserted on both
    /// surfaces above). It must never re-read the deleted resource.
    /// </summary>
    [Fact]
    public void Delete_PollsTheOperation_AndFinishesOnTheLocationResult()
    {
        var record = RecordFromCli();
        int deleteIndex = record.Entries.FindIndex(e => e.Request.Method == "DELETE");

        Assert.True(deleteIndex >= 0, "the scenario never issued a DELETE");
        Assert.Collection(
            record.Entries.Skip(deleteIndex + 1),
            poll => Assert.Contains("/operations/nic-delete", poll.Request.Uri),
            result =>
            {
                // The recorder redacts the token after /operationResults/.
                Assert.Contains("/operationResults/", result.Request.Uri);
                Assert.Equal(204, result.Response.StatusCode);
            });
    }

    /// <summary>
    /// Runs the real cmdlet classes in a runspace, the layer <c>ScenarioHost.RunPowerShell</c>
    /// bypasses: the hashtable tag, string[] DNS servers and switch must convert into what the
    /// processor binds, Get must route by parameter set and enumerate the list, and Remove's
    /// -Force must skip the High-impact prompt that a host without UI cannot answer.
    /// </summary>
    [Fact]
    public void PowerShellCmdlets_ConvertTheirParameters_AndRouteParameterSets()
    {
        using var host = new ScenarioHost(
            ClientType.PowerShell,
            new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()),
            new FakeNetworkInterfaceArmBackend(SubscriptionId, ResourceGroupName),
            extensions: [ExtensionUnderTest.Network]);

        var sessionState = InitialSessionState.CreateDefault2();
        sessionState.Commands.Add(new SessionStateCmdletEntry("New-AzNetworkInterface", typeof(NewNetworkInterfaceCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Get-AzNetworkInterface", typeof(GetNetworkInterfaceCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Remove-AzNetworkInterface", typeof(RemoveNetworkInterfaceCmdlet), null));
        using var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();

        AssertNic(Assert.Single(Invoke(runspace, "New-AzNetworkInterface", new(PsCreateParameters())
        {
            ["DnsServer"] = new[] { "10.0.0.4", "10.0.0.5" },
            ["Tag"] = new Hashtable { ["env"] = "scenario" },
        })));

        // List is the default parameter set and enumerates, so the one NIC is one pipeline object.
        AssertNic(Assert.Single(Invoke(runspace, "Get-AzNetworkInterface", PsListParameters())));
        AssertNic(Assert.Single(Invoke(runspace, "Get-AzNetworkInterface", PsShowParameters())));

        // The delete itself writes nothing, so -PassThru's true is the only output.
        var removed = Invoke(runspace, "Remove-AzNetworkInterface", new(PsDeleteParameters()) { ["Force"] = true, ["PassThru"] = true });
        Assert.True(Assert.IsType<bool>(Assert.Single(removed)));
    }

    private static List<object> Invoke(Runspace runspace, string command, Dictionary<string, object> parameters)
    {
        using var powerShell = PowerShell.Create();
        powerShell.Runspace = runspace;
        var outputs = powerShell.AddCommand(command).AddParameters(parameters).Invoke();

        Assert.Empty(powerShell.Streams.Error);
        return outputs.Select(output => output.BaseObject).ToList();
    }

    /// <summary>Records the whole lifecycle from the CLI surface against the fake ARM backend.</summary>
    private static SessionRecord RecordFromCli()
    {
        var record = new SessionRecord();
        using var host = new ScenarioHost(
            ClientType.CLI,
            new RecordPlaybackInterceptor(RecorderMode.Record, record),
            new FakeNetworkInterfaceArmBackend(SubscriptionId, ResourceGroupName),
            extensions: [ExtensionUnderTest.Network]);

        AssertCreated(host.RunCli(CreateArgs()));
        AssertShown(host.RunCli(ShowArgs()));
        AssertListed(host.RunCli(ListArgs()));
        Assert.Empty(host.RunCli(DeleteArgs()));
        return record;
    }

    private static string[] CreateArgs() =>
    [
        "network", "nic", "create",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--name", NicName,
        "--location", Location,
        "--vnet-name", "scenario-vnet",
        "--subnet", "default",
        "--public-ip-address", "scenario-pip",
        "--network-security-group", "scenario-nsg",
        "--private-ip-address", "10.0.0.10",
        "--accelerated-networking", "true",
        "--dns-servers", "10.0.0.4", "10.0.0.5",
        "--tags", "env=scenario",
    ];

    private static string[] ShowArgs() =>
    [
        "network", "nic", "show",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--name", NicName,
        "--expand", Expand,
    ];

    private static string[] ListArgs() =>
    [
        "network", "nic", "list",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
    ];

    private static string[] DeleteArgs() =>
    [
        "network", "nic", "delete",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--name", NicName,
    ];

    private static Dictionary<string, object> PsCreateParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = NicName,
        ["Location"] = Location,
        ["SubnetId"] = SubnetId,
        ["PublicIpAddressId"] = PublicIpId,
        ["NetworkSecurityGroupId"] = NsgId,
        ["PrivateIpAddress"] = "10.0.0.10",
        ["EnableAcceleratedNetworking"] = true,
        ["DnsServer"] = new List<string> { "10.0.0.4", "10.0.0.5" },
        ["Tag"] = new List<string> { "env=scenario" },
    };

    private static Dictionary<string, object> PsShowParameters() => new(PsDeleteParameters())
    {
        ["ExpandResource"] = Expand,
    };

    private static Dictionary<string, object> PsListParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
    };

    private static Dictionary<string, object> PsDeleteParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = NicName,
    };

    private static void AssertCreated(IReadOnlyList<object> outputs) => AssertNic(Assert.Single(outputs));

    private static void AssertShown(IReadOnlyList<object> outputs) => AssertNic(Assert.Single(outputs));

    private static void AssertListed(IReadOnlyList<object> outputs)
    {
        var interfaces = Assert.IsType<List<NetworkInterface>>(Assert.Single(outputs));
        AssertNic(Assert.Single(interfaces));
    }

    private static void AssertNic(object output)
    {
        var nic = Assert.IsType<NetworkInterface>(output);
        Assert.Equal(NicName, nic.Name);
        Assert.Equal(Location, nic.Location);
        Assert.Equal("Succeeded", nic.ProvisioningState);
        Assert.Equal("scenario", nic.Tags?["env"]);
        Assert.Equal(NsgId, nic.NetworkSecurityGroupId);
        Assert.True(nic.EnableAcceleratedNetworking);
        Assert.Null(nic.EnableIPForwarding);
        Assert.Equal(["10.0.0.4", "10.0.0.5"], nic.DnsSettingsDnsServers ?? []);

        // The legacy IP configuration defaults the customization applies, echoed back by the fake.
        var ipConfiguration = Assert.Single(nic.IpConfigurations ?? []);
        Assert.Equal("ipconfig1", ipConfiguration.Name);
        Assert.Equal(SubnetId, ipConfiguration.SubnetId);
        Assert.Equal(PublicIpId, ipConfiguration.PublicIpAddressId);
        Assert.Equal("10.0.0.10", ipConfiguration.PrivateIPAddress);
        Assert.Equal("Static", ipConfiguration.PrivateIPAllocationMethod);
    }
}
