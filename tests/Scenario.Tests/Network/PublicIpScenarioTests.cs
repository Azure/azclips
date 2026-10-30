using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.Json;
using Core.Abstractions;
using Tests.Recording;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Scenario.Tests;

/// <summary>
/// Exercises the public IP lifecycle through the real dispatcher/binder and ARM request pipeline on
/// both client surfaces. Unlike <see cref="VNetScenarioTests"/> this records against
/// <see cref="FakePublicIpArmBackend"/> rather than live Azure, so there is no committed recording
/// and no credentials are needed; the record is built in memory and replayed immediately.
/// <para>
/// The PowerShell replay uses a body-matching matcher, so it fails unless the PS parameter surface
/// produces byte-identical requests to the CLI one. That is the cross-surface parity gate: the
/// <c>ExtensionIndex</c> PS metadata binds arguments to processor properties by string, and a
/// mismatch there builds fine and would otherwise only surface at runtime.
/// </para>
/// <para>
/// Not covered here, exactly as for virtual networks: the thin cmdlet classes under
/// <c>src/PS/Network/Cmdlets</c>. <c>ScenarioHost.RunPowerShell</c> drives the dispatcher with a
/// parameter dictionary rather than instantiating the cmdlet, so their <c>BoundParameters</c> guards
/// and <c>Hashtable</c> flattening have no automated coverage on either command group.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class PublicIpScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-pip-rg";
    private const string PipName = "scenario-pip";
    private const string Location = "westus2";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttachedPublicIpAddress_FormatsItsAddressAndConfiguration(bool expanded)
    {
        var configuration = expanded
            ? """{ "id": "/nic/ipConfigurations/ipconfig1", "properties": { "privateIPAddress": "10.0.0.4" } }"""
            : """{ "id": "/nic/ipConfigurations/ipconfig1" }""";
        var pip = JsonSerializer.Deserialize<PublicIpAddress>($$"""
            {
              "name": "my-pip",
              "properties": {
                "ipAddress": "20.1.2.3",
                "ipConfiguration": {{configuration}}
              }
            }
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        using var runspace = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault2());
        runspace.Open();
        using var powerShell = PowerShell.Create();
        powerShell.Runspace = runspace;

        var rendered = Assert.Single(powerShell.AddCommand("Format-List").AddParameter("InputObject", pip)
            .AddCommand("Out-String").Invoke<string>());

        Assert.Empty(powerShell.Streams.Error);
        Assert.Matches(@"IpAddress\s+: 20\.1\.2\.3", rendered);
        Assert.Contains("/nic/ipConfigurations/ipconfig1", rendered);
        Assert.DoesNotMatch(@"(?m)^Properties\s+:", rendered);
        if (expanded)
        {
            powerShell.Commands.Clear();
            var expandedRendered = Assert.Single(powerShell.AddCommand("Format-List")
                .AddParameter("InputObject", pip.IpConfiguration).AddCommand("Out-String").Invoke<string>());
            Assert.Empty(powerShell.Streams.Error);
            Assert.Matches(@"PrivateIpAddress\s+: 10\.0\.0\.4", expandedRendered);
        }
    }

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
            cli.RunCli(DeleteArgs());
        }

        using (var ps = new ScenarioHost(
            ClientType.PowerShell,
            new RecordPlaybackInterceptor(RecorderMode.Playback, record, new DefaultRequestMatcher(matchBody: true)),
            extensions: [ExtensionUnderTest.Network]))
        {
            AssertCreated(ps.RunPowerShell("New-AzPublicIpAddress", PsCreateParameters(), extension: "Network"));
            AssertShown(ps.RunPowerShell("Get-AzPublicIpAddress_Get", PsShowParameters(), extension: "Network"));
            AssertListed(ps.RunPowerShell("Get-AzPublicIpAddress_List", PsListParameters(), extension: "Network"));
            ps.RunPowerShell("Remove-AzPublicIpAddress", PsShowParameters(), extension: "Network");
        }
    }

    /// <summary>
    /// The create must PUT the legacy shape: flattened arguments land in the right envelope slots,
    /// the SKU/version/idle-timeout defaults are applied, and the LRO really is a 202 plus a poll.
    /// </summary>
    [Fact]
    public void Create_PutsTheLegacyBody_AndPollsTheAsyncOperation()
    {
        var record = RecordFromCli();
        var create = record.Entries.First(e => e.Request.Method == "PUT");

        Assert.Contains($"/publicIPAddresses/{PipName}", create.Request.Uri);
        Assert.Contains("api-version=2025-07-01", create.Request.Uri);

        var body = create.Request.Body!;
        Assert.Contains("\"publicIPAllocationMethod\":\"Static\"", body);
        Assert.Contains("\"publicIPAddressVersion\":\"IPv4\"", body);
        Assert.Contains("\"idleTimeoutInMinutes\":10", body);
        Assert.Contains("\"domainNameLabel\":\"scenario-pip-label\"", body);
        Assert.Contains("\"ipTagType\":\"FirstPartyUsage\"", body);
        Assert.Contains("\"tag\":\"/Sql\"", body);
        Assert.Contains("\"name\":\"Standard\"", body);
        Assert.Contains("\"env\":\"scenario\"", body);
        Assert.Contains("\"zones\":[\"1\",\"2\",\"3\"]", body);

        // Nothing the caller did not ask for may reach the wire.
        Assert.DoesNotContain("ddosSettings", body);
        Assert.DoesNotContain("publicIPPrefix", body);
        Assert.DoesNotContain("reverseFqdn", body);

        Assert.Contains(record.Entries, e => e.Request.Uri.Contains("/operations/pip-create"));
    }

    /// <summary>
    /// The delete polls the operation URI from the Location header and must never re-read the
    /// deleted resource: falling back to the DELETE target would GET a resource that is gone.
    /// </summary>
    [Fact]
    public void Delete_PollsTheLocationHeader_AndNeverRereadsTheResource()
    {
        var record = RecordFromCli();
        int deleteIndex = record.Entries.FindIndex(e => e.Request.Method == "DELETE");

        Assert.True(deleteIndex >= 0, "the scenario never issued a DELETE");
        var afterDelete = record.Entries.Skip(deleteIndex + 1).ToList();

        Assert.NotEmpty(afterDelete);
        Assert.All(afterDelete, e => Assert.Contains("/operations/pip-delete", e.Request.Uri));
    }

    /// <summary>Records the whole lifecycle from the CLI surface against the fake ARM backend.</summary>
    private static SessionRecord RecordFromCli()
    {
        var record = new SessionRecord();
        using var host = new ScenarioHost(
            ClientType.CLI,
            new RecordPlaybackInterceptor(RecorderMode.Record, record),
            new FakePublicIpArmBackend(SubscriptionId, ResourceGroupName),
            extensions: [ExtensionUnderTest.Network]);

        AssertCreated(host.RunCli(CreateArgs()));
        AssertShown(host.RunCli(ShowArgs()));
        AssertListed(host.RunCli(ListArgs()));
        host.RunCli(DeleteArgs());
        return record;
    }

    private static string[] CreateArgs() =>
    [
        "network", "public-ip", "create",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--name", PipName,
        "--location", Location,
        "--sku", "Standard",
        "--idle-timeout", "10",
        "--dns-name", "scenario-pip-label",
        "--zone", "1", "2", "3",
        "--ip-tags", "FirstPartyUsage=/Sql",
        "--tags", "env=scenario",
    ];

    private static string[] ShowArgs() =>
    [
        "network", "public-ip", "show",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--name", PipName,
    ];

    private static string[] ListArgs() =>
    [
        "network", "public-ip", "list",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
    ];

    private static string[] DeleteArgs() =>
    [
        "network", "public-ip", "delete",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--name", PipName,
    ];

    private static Dictionary<string, object> PsCreateParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = PipName,
        ["Location"] = Location,
        ["Sku"] = "Standard",
        ["IdleTimeoutInMinutes"] = 10,
        ["DomainNameLabel"] = "scenario-pip-label",
        ["Zone"] = new List<string> { "1", "2", "3" },
        ["IpTag"] = new List<string> { "FirstPartyUsage=/Sql" },
        ["Tag"] = new List<string> { "env=scenario" },
    };

    private static Dictionary<string, object> PsShowParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = PipName,
    };

    private static Dictionary<string, object> PsListParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
    };

    private static void AssertCreated(IReadOnlyList<object> outputs) => AssertPip(Assert.Single(outputs));

    private static void AssertShown(IReadOnlyList<object> outputs) => AssertPip(Assert.Single(outputs));

    private static void AssertListed(IReadOnlyList<object> outputs)
    {
        var addresses = Assert.IsType<List<PublicIpAddress>>(Assert.Single(outputs));
        AssertPip(Assert.Single(addresses));
    }

    private static void AssertPip(object output)
    {
        var pip = Assert.IsType<PublicIpAddress>(output);
        Assert.Equal(PipName, pip.Name);
        Assert.Equal(Location, pip.Location);
        Assert.Equal("Succeeded", pip.ProvisioningState);
        Assert.Equal("scenario", pip.Tags?["env"]);
        Assert.Equal(["1", "2", "3"], pip.Zones ?? []);

        // The legacy defaults the customization applies, echoed back by the fake from the PUT body.
        Assert.Equal("Standard", pip.Sku?.Name);
        Assert.Equal("Static", pip.PublicIpAllocationMethod);
        Assert.Equal("IPv4", pip.PublicIpAddressVersion);
        Assert.Equal(10, pip.IdleTimeoutInMinutes);
        Assert.Equal("scenario-pip-label", pip.DnsSettings?.DomainNameLabel);

        var ipTag = Assert.Single(pip.IpTags ?? []);
        Assert.Equal("FirstPartyUsage", ipTag.IpTagType);
        Assert.Equal("/Sql", ipTag.Tag);
    }
}
