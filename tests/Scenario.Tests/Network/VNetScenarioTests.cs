using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Core.Abstractions;
using Tests.Recording;
using Microsoft.Azure.Azclips.Cmdlets.Network;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises the virtual network lifecycle through the real dispatcher/binder and ARM request
/// pipeline on both client surfaces. Record mode captures the CLI dispatcher and real PowerShell
/// cmdlet workflows against live Azure; playback replays the recordings without credentials.
/// <para>
/// The create covers the parameters that legacy <c>az network vnet create</c> and
/// <c>New-AzVirtualNetwork</c> callers rely on: a compound subnet declaration, DNS servers,
/// tags, and a flow timeout.
/// </para>
/// <para>
/// The request-shape and delete-polling tests record in memory against
/// <see cref="FakeVirtualNetworkArmBackend"/>, which answers like the committed live recording.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class VNetScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-vnet-rg";
    private const string VNetName = "scenario-vnet";
    private const string Location = "westus2";

    [Fact]
    public void CreateShowListDeleteVirtualNetwork()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(VNetScenarioTests), nameof(CreateShowListDeleteVirtualNetwork));

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                useLiveAuth: true,
                extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]);

            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                host.RunCli(GroupArgs(subscriptionId, groupName));
                AssertCreated(host.RunCli(CreateArgs(subscriptionId, groupName)));
                AssertShown(host.RunCli(ShowArgs(subscriptionId, groupName)));
                AssertListed(host.RunCli(ListArgs(subscriptionId, groupName)));
                Assert.Empty(host.RunCli(DeleteArgs(subscriptionId, groupName)));
                recording.Save();
            }
            finally
            {
                // After the save, so the delete never lands in the recording.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
            }
            return;
        }

        using (var cli = new ScenarioHost(
            ClientType.CLI,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]))
        {
            cli.RunCli(GroupArgs(SubscriptionId));
            AssertCreated(cli.RunCli(CreateArgs(SubscriptionId)));
            AssertShown(cli.RunCli(ShowArgs(SubscriptionId)));
            AssertListed(cli.RunCli(ListArgs(SubscriptionId)));
            Assert.Empty(cli.RunCli(DeleteArgs(SubscriptionId)));
        }

        using (var ps = new ScenarioHost(
            ClientType.PowerShell,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(), extension: "Resources");
            AssertCreated(ps.RunPowerShell("New-AzVirtualNetwork", PsCreateParameters(), extension: "Network"));
            AssertShown(ps.RunPowerShell("Get-AzVirtualNetwork_Get", PsShowParameters(), extension: "Network"));
            AssertListed(ps.RunPowerShell("Get-AzVirtualNetwork_List", PsListParameters(), extension: "Network"));
            Assert.Empty(ps.RunPowerShell("Remove-AzVirtualNetwork", PsShowParameters(), extension: "Network"));
        }
    }

    /// <summary>
    /// The create must PUT only what the caller asked for, against the pinned api-version, and
    /// poll the Azure-AsyncOperation before the runtime re-reads the resource.
    /// </summary>
    [Fact]
    public void Create_PutsTheLegacyBody_AndPollsTheAsyncOperation()
    {
        var record = RecordFromFakeCli();
        var create = record.Entries.First(e => e.Request.Method == "PUT");

        Assert.Contains($"/virtualNetworks/{VNetName}", create.Request.Uri);
        Assert.Contains("api-version=2025-07-01", create.Request.Uri);
        Assert.Equal(201, create.Response.StatusCode);

        var body = create.Request.Body!;
        Assert.Contains("\"addressPrefixes\":[\"10.0.0.0/16\"]", body);
        Assert.Contains("\"dnsServers\":[\"10.0.0.4\",\"10.0.0.5\"]", body);
        Assert.Contains("\"flowTimeoutInMinutes\":10", body);
        Assert.Contains("\"env\":\"scenario\"", body);
        Assert.Contains("\"name\":\"web\"", body);
        Assert.Contains("\"defaultOutboundAccess\":false", body);
        Assert.Contains("\"service\":\"Microsoft.Storage\"", body);

        // Both subnet network policies are optional; the RP default applies unless a flag sets them.
        Assert.DoesNotContain("privateEndpointNetworkPolicies", body);
        Assert.DoesNotContain("privateLinkServiceNetworkPolicies", body);

        var createIndex = record.Entries.IndexOf(create);
        Assert.Contains("/operations/vnet-create", record.Entries[createIndex + 1].Request.Uri);
        Assert.Contains($"/virtualNetworks/{VNetName}", record.Entries[createIndex + 2].Request.Uri);
    }

    /// <summary>
    /// Like the network RP, the delete polls the Azure-AsyncOperation status and finishes on the
    /// Location result, an empty 204, so a finished delete prints nothing. It must never re-read
    /// the deleted resource.
    /// </summary>
    [Fact]
    public void Delete_PollsTheOperation_AndFinishesOnTheLocationResult()
    {
        var record = RecordFromFakeCli();
        int deleteIndex = record.Entries.FindIndex(e => e.Request.Method == "DELETE");

        Assert.True(deleteIndex >= 0, "the scenario never issued a DELETE");
        Assert.Collection(
            record.Entries.Skip(deleteIndex + 1),
            poll => Assert.Contains("/operations/vnet-delete", poll.Request.Uri),
            result =>
            {
                Assert.Contains("/operationResults/", result.Request.Uri);
                Assert.Equal(204, result.Response.StatusCode);
            });
    }

    /// <summary>
    /// Runs the real cmdlet classes in a runspace, the layer <c>ScenarioHost.RunPowerShell</c>
    /// bypasses: subnet hashtables (including array values) and the tag hashtable must convert into
    /// the tokens the processor binds, Get must route by parameter set and enumerate the list, and
    /// Remove's -Force must skip the High-impact prompt that a host without UI cannot answer.
    /// </summary>
    [Fact]
    public void PowerShellCmdlets_ConvertTheirParameters_AndRouteParameterSets()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(VNetScenarioTests),
            nameof(PowerShellCmdlets_ConvertTheirParameters_AndRouteParameterSets),
            new DefaultRequestMatcher(matchBody: true));
        var isRecording = recording.Mode == RecorderMode.Record;
        var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
        using var host = new ScenarioHost(
            ClientType.PowerShell,
            recording.CreateInterceptor(),
            useLiveAuth: isRecording,
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]);
        var subscriptionId = isRecording ? host.LiveSubscriptionId() : SubscriptionId;
        var identity = new Dictionary<string, object>
        {
            ["SubscriptionId"] = subscriptionId,
            ["ResourceGroupName"] = groupName,
            ["ResourceName"] = VNetName,
        };

        var sessionState = InitialSessionState.CreateDefault2();
        sessionState.Commands.Add(new SessionStateCmdletEntry("New-AzVirtualNetwork", typeof(NewVirtualNetworkCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Get-AzVirtualNetwork", typeof(GetVirtualNetworkCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Remove-AzVirtualNetwork", typeof(RemoveVirtualNetworkCmdlet), null));
        using var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();

        try
        {
            host.RunPowerShell("New-AzResourceGroup", PsGroupParameters(subscriptionId, groupName), extension: "Resources");
            AssertVNet(Assert.Single(Invoke(runspace, "New-AzVirtualNetwork", new(identity)
            {
                ["Location"] = Location,
                ["AddressPrefix"] = new[] { "10.0.0.0/16" },
                ["DnsServer"] = new[] { "10.0.0.4", "10.0.0.5" },
                ["FlowTimeout"] = 10,
                ["Tag"] = new Hashtable { ["env"] = "scenario" },
                ["Subnet"] = new object[]
                {
                    new Hashtable { ["Name"] = "web", ["AddressPrefixes"] = new[] { "10.0.1.0/24" }, ["DefaultOutboundAccess"] = false },
                    new Hashtable { ["Name"] = "data", ["AddressPrefixes"] = new[] { "10.0.2.0/24" }, ["ServiceEndpoint"] = "Microsoft.Storage" },
                },
            })));

            // List is the default parameter set and enumerates, so the one VNet is one pipeline object.
            AssertVNet(Assert.Single(Invoke(runspace, "Get-AzVirtualNetwork", PsListParameters(subscriptionId, groupName))));
            AssertVNet(Assert.Single(Invoke(runspace, "Get-AzVirtualNetwork", identity)));

            // The delete itself writes nothing, so -PassThru's true is the only output.
            var removed = Invoke(runspace, "Remove-AzVirtualNetwork", new(identity) { ["Force"] = true, ["PassThru"] = true });
            Assert.True(Assert.IsType<bool>(Assert.Single(removed)));
            recording.Save();
        }
        finally
        {
            if (isRecording)
            {
                // After the save, so cleanup never lands in the recording.
                try
                {
                    host.RunPowerShell("Remove-AzResourceGroup", new Dictionary<string, object>
                    {
                        ["SubscriptionId"] = subscriptionId,
                        ["Name"] = groupName,
                    }, extension: "Resources");
                }
                catch { }
            }
        }
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
    private static SessionRecord RecordFromFakeCli()
    {
        var record = new SessionRecord();
        using var host = new ScenarioHost(
            ClientType.CLI,
            new RecordPlaybackInterceptor(RecorderMode.Record, record),
            new FakeVirtualNetworkArmBackend(SubscriptionId, ResourceGroupName),
            extensions: [ExtensionUnderTest.Network]);

        AssertCreated(host.RunCli(CreateArgs(SubscriptionId)));
        AssertShown(host.RunCli(ShowArgs(SubscriptionId)));
        AssertListed(host.RunCli(ListArgs(SubscriptionId)));
        Assert.Empty(host.RunCli(DeleteArgs(SubscriptionId)));
        return record;
    }

    private static string[] GroupArgs(string subscriptionId, string? groupName = null) =>
    [
        "group", "create",
        "--subscription-id", subscriptionId,
        "--name", groupName ?? ResourceGroupName,
        "--location", Location,
    ];

    private static string[] CreateArgs(string subscriptionId, string? groupName = null) =>
    [
        "network", "vnet", "create",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName ?? ResourceGroupName,
        "--name", VNetName,
        "--location", Location,
        "--address-prefixes", "10.0.0.0/16",
        "--dns-servers", "10.0.0.4", "10.0.0.5",
        "--tags", "env=scenario",
        "--flowtimeout", "10",
        "--subnets", "name=web", "address-prefixes=10.0.1.0/24", "default-outbound-access=false",
        "--subnets", "name=data", "address-prefixes=10.0.2.0/24", "service-endpoints=Microsoft.Storage",
    ];

    private static string[] ShowArgs(string subscriptionId, string? groupName = null) =>
    [
        "network", "vnet", "show",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName ?? ResourceGroupName,
        "--name", VNetName,
    ];

    private static string[] ListArgs(string subscriptionId, string? groupName = null) =>
    [
        "network", "vnet", "list",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName ?? ResourceGroupName,
    ];

    private static string[] DeleteArgs(string subscriptionId, string? groupName = null) =>
    [
        "network", "vnet", "delete",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName ?? ResourceGroupName,
        "--name", VNetName,
    ];

    private static Dictionary<string, object> PsGroupParameters(string subscriptionId = SubscriptionId, string groupName = ResourceGroupName) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["Name"] = groupName,
        ["Location"] = Location,
    };

    private static Dictionary<string, object> PsCreateParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = VNetName,
        ["Location"] = Location,
        ["AddressPrefix"] = new List<string> { "10.0.0.0/16" },
        ["DnsServer"] = new List<string> { "10.0.0.4", "10.0.0.5" },
        ["Tag"] = new List<string> { "env=scenario" },
        ["FlowTimeout"] = 10,
        ["Subnet"] = new List<List<string>>
        {
            new() { "Name=web", "AddressPrefixes=10.0.1.0/24", "DefaultOutboundAccess=false" },
            new() { "Name=data", "AddressPrefixes=10.0.2.0/24", "ServiceEndpoint=Microsoft.Storage" },
        },
    };

    private static Dictionary<string, object> PsShowParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = VNetName,
    };

    private static Dictionary<string, object> PsListParameters(string subscriptionId = SubscriptionId, string groupName = ResourceGroupName) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = groupName,
    };

    private static void AssertCreated(IReadOnlyList<object> outputs) => AssertVNet(Assert.Single(outputs));

    private static void AssertShown(IReadOnlyList<object> outputs) => AssertVNet(Assert.Single(outputs));

    private static void AssertListed(IReadOnlyList<object> outputs)
    {
        var vnets = Assert.IsType<List<VirtualNetwork>>(Assert.Single(outputs));
        AssertVNet(Assert.Single(vnets));
    }

    private static void AssertVNet(object output)
    {
        var vnet = Assert.IsType<VirtualNetwork>(output);
        Assert.Equal(VNetName, vnet.Name);
        Assert.Equal(Location, vnet.Location);
        Assert.Equal("Succeeded", vnet.Properties.ProvisioningState);
        Assert.Equal("scenario", vnet.Tags?["env"]);
        Assert.Equal("10.0.0.0/16", Assert.Single(vnet.Properties.AddressSpace.AddressPrefixes));
        Assert.Equal(10, vnet.Properties.FlowTimeoutInMinutes);
        Assert.Equal(["10.0.0.4", "10.0.0.5"], vnet.Properties.DhcpOptions?.DnsServers ?? []);

        Assert.Collection(
            vnet.Properties.Subnets,
            web =>
            {
                Assert.Equal("web", web.Name);
                Assert.Equal("10.0.1.0/24", web.Properties.AddressPrefix);
                Assert.False(web.Properties.DefaultOutboundAccess);
            },
            data =>
            {
                Assert.Equal("data", data.Name);
                Assert.Equal("10.0.2.0/24", data.Properties.AddressPrefix);
                Assert.Equal("Microsoft.Storage", Assert.Single(data.Properties.ServiceEndpoints).Service);
            });
    }
}
