using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.RegularExpressions;
using Core.Abstractions;
using Tests.Recording;
using Microsoft.Azure.Azclips.Cmdlets.Network;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises the public IP lifecycle through the real dispatcher/binder and ARM request pipeline on
/// both client surfaces. Record mode captures the CLI dispatcher and real PowerShell cmdlet
/// workflows against live Azure; playback replays the recordings without credentials.
/// <para>
/// The create covers the parameters that legacy <c>az network public-ip create</c> and
/// <c>New-AzPublicIpAddress</c> callers rely on: SKU, idle timeout, DNS label, zones, an IP tag,
/// and <c>KEY[=VALUE]</c> tags including a bare key.
/// </para>
/// <para>
/// The request-shape and delete-polling tests record in memory against
/// <see cref="FakePublicIpArmBackend"/>, which answers like the committed live recording.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class PublicIpScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-pip-rg";
    private const string PipName = "scenario-pip";
    private const string DnsLabel = "scenario-pip-label";
    private const string Location = "westus2";
    private const string AttachedGroupName = "scenario-pip-nic-rg";
    private const string AttachedVnetName = "scenario-pip-vnet";
    private const string AttachedNicName = "scenario-pip-nic";

    [Fact]
    public void CreateShowListDeletePublicIpAddress()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(PublicIpScenarioTests), nameof(CreateShowListDeletePublicIpAddress));

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
            // DNS labels are unique per region, so the live one is scoped to the run as well.
            var dnsLabel = TestRun.ScopedName(DnsLabel, recording.Mode);
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                useLiveAuth: true,
                extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]);

            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                host.RunCli(GroupArgs(subscriptionId, groupName));
                AssertCreated(host.RunCli(CreateArgs(subscriptionId, groupName, dnsLabel)), dnsLabel);
                AssertShown(host.RunCli(ShowArgs(subscriptionId, groupName)), dnsLabel);
                AssertListed(host.RunCli(ListArgs(subscriptionId, groupName)), dnsLabel);
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
            AssertCreated(ps.RunPowerShell("New-AzPublicIpAddress", PsCreateParameters(), extension: "Network"));
            AssertShown(ps.RunPowerShell("Get-AzPublicIpAddress_Get", PsShowParameters(), extension: "Network"));
            AssertListed(ps.RunPowerShell("Get-AzPublicIpAddress_List", PsListParameters(), extension: "Network"));
            Assert.Empty(ps.RunPowerShell("Remove-AzPublicIpAddress", PsShowParameters(), extension: "Network"));
        }
    }

    /// <summary>
    /// The create must PUT only what the caller asked for plus the legacy defaults, against the
    /// pinned api-version, and poll the Azure-AsyncOperation before the runtime re-reads the resource.
    /// </summary>
    [Fact]
    public void Create_PutsTheLegacyBody_AndPollsTheAsyncOperation()
    {
        var record = RecordFromFakeCli();
        var create = record.Entries.First(e => e.Request.Method == "PUT");

        Assert.Contains($"/publicIPAddresses/{PipName}", create.Request.Uri);
        Assert.Contains("api-version=2025-07-01", create.Request.Uri);
        Assert.Equal(201, create.Response.StatusCode);

        var body = create.Request.Body!;
        Assert.Contains("\"publicIPAllocationMethod\":\"Static\"", body);
        Assert.Contains("\"publicIPAddressVersion\":\"IPv4\"", body);
        Assert.Contains("\"idleTimeoutInMinutes\":10", body);
        Assert.Contains($"\"domainNameLabel\":\"{DnsLabel}\"", body);
        Assert.Contains("\"ipTagType\":\"RoutingPreference\"", body);
        Assert.Contains("\"tag\":\"Internet\"", body);
        Assert.Contains("\"name\":\"Standard\"", body);
        Assert.Contains("\"tags\":{\"env\":\"scenario\",\"temporary\":\"\"}", body);
        Assert.Contains("\"zones\":[\"1\",\"2\",\"3\"]", body);

        // Nothing the caller did not ask for may reach the wire.
        Assert.DoesNotContain("ddosSettings", body);
        Assert.DoesNotContain("publicIPPrefix", body);
        Assert.DoesNotContain("reverseFqdn", body);

        var createIndex = record.Entries.IndexOf(create);
        Assert.Contains("/operations/pip-create", record.Entries[createIndex + 1].Request.Uri);
        Assert.Contains($"/publicIPAddresses/{PipName}", record.Entries[createIndex + 2].Request.Uri);
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
            poll => Assert.Contains("/operations/pip-delete", poll.Request.Uri),
            result =>
            {
                Assert.Contains("/operationResults/", result.Request.Uri);
                Assert.Equal(204, result.Response.StatusCode);
            });
    }

    /// <summary>
    /// Runs the real cmdlet classes in a runspace, the layer <c>ScenarioHost.RunPowerShell</c>
    /// bypasses: the tag and IP tag hashtables must convert into the tokens the processor binds,
    /// Get must route by parameter set and enumerate the list, and Remove's -Force must skip the
    /// High-impact prompt that a host without UI cannot answer.
    /// </summary>
    [Fact]
    public void PowerShellCmdlets_ConvertTheirParameters_AndRouteParameterSets()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(PublicIpScenarioTests),
            nameof(PowerShellCmdlets_ConvertTheirParameters_AndRouteParameterSets),
            new DefaultRequestMatcher(matchBody: true));
        var isRecording = recording.Mode == RecorderMode.Record;
        var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
        var dnsLabel = TestRun.ScopedName("scenario-pip-ps-label", recording.Mode);
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
            ["ResourceName"] = PipName,
        };

        var sessionState = InitialSessionState.CreateDefault2();
        sessionState.Commands.Add(new SessionStateCmdletEntry("New-AzPublicIpAddress", typeof(NewPublicIpAddressCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Get-AzPublicIpAddress", typeof(GetPublicIpAddressCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Remove-AzPublicIpAddress", typeof(RemovePublicIpAddressCmdlet), null));
        using var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();

        try
        {
            host.RunPowerShell("New-AzResourceGroup", PsGroupParameters(subscriptionId, groupName), extension: "Resources");

            // One key per hashtable: Hashtable order is per-process, and this recording matches bodies byte for byte.
            var created = Assert.IsType<PublicIpAddress>(Assert.Single(Invoke(runspace, "New-AzPublicIpAddress", new(identity)
            {
                ["Location"] = Location,
                ["Sku"] = "Standard",
                ["DomainNameLabel"] = dnsLabel,
                ["Zone"] = new[] { "1", "2", "3" },
                ["IpTag"] = new Hashtable { ["RoutingPreference"] = "Internet" },
                ["Tag"] = new Hashtable { ["env"] = "scenario" },
            })));
            Assert.Equal("scenario", created.Tags?["env"]);
            var ipTag = Assert.Single(created.IpTags ?? []);
            Assert.Equal("RoutingPreference", ipTag.IpTagType);
            Assert.Equal("Internet", ipTag.Tag);

            // List is the default parameter set and enumerates, so the one address is one pipeline object.
            var listed = Assert.IsType<PublicIpAddress>(Assert.Single(Invoke(runspace, "Get-AzPublicIpAddress", PsListParameters(subscriptionId, groupName))));
            Assert.Equal(PipName, listed.Name);
            var shown = Assert.IsType<PublicIpAddress>(Assert.Single(Invoke(runspace, "Get-AzPublicIpAddress", identity)));
            Assert.Equal(PipName, shown.Name);
            Assert.False(string.IsNullOrEmpty(shown.IpAddress));

            // The delete itself writes nothing, so -PassThru's true is the only output.
            var removed = Invoke(runspace, "Remove-AzPublicIpAddress", new(identity) { ["Force"] = true, ["PassThru"] = true });
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

    /// <summary>
    /// Issue #1117: once a NIC holds the address, ARM returns <c>ipConfiguration</c>, and
    /// <c>Get-AzPublicIpAddress</c> used to render a blank <c>Properties</c> without the IP. PowerShell
    /// must show the address and the configuration at the top level, with and without the expand.
    /// Recorded from the CLI against live Azure; the CLI JSON envelope is covered in Cli.Tests.
    /// </summary>
    [Fact]
    public void AttachedPublicIpAddress_ShowsItsAddressAndIpConfiguration()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(PublicIpScenarioTests), nameof(AttachedPublicIpAddress_ShowsItsAddressAndIpConfiguration));
        var isRecording = recording.Mode == RecorderMode.Record;
        var groupName = TestRun.ScopedName(AttachedGroupName, recording.Mode);

        using (var cli = new ScenarioHost(
            ClientType.CLI,
            recording.CreateInterceptor(),
            useLiveAuth: isRecording,
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]))
        {
            var subscriptionId = isRecording ? cli.LiveSubscriptionId() : SubscriptionId;
            try
            {
                cli.RunCli(GroupArgs(subscriptionId, groupName));
                cli.RunCli(
                    "network", "vnet", "create", "--subscription-id", subscriptionId, "-g", groupName, "-n", AttachedVnetName, "-l", Location,
                    "--address-prefixes", "10.0.0.0/16", "--subnet-names", "default", "--subnet-address-prefixes", "10.0.0.0/24");
                cli.RunCli(
                    "network", "public-ip", "create", "--subscription-id", subscriptionId, "-g", groupName, "-n", PipName, "-l", Location,
                    "--sku", "Standard");
                cli.RunCli(
                    "network", "nic", "create", "--subscription-id", subscriptionId, "-g", groupName, "-n", AttachedNicName, "-l", Location,
                    "--vnet-name", AttachedVnetName, "--subnet", "default", "--public-ip-address", PipName);

                var shown = Assert.IsType<PublicIpAddress>(Assert.Single(cli.RunCli(ShowArgs(subscriptionId, groupName))));
                AssertAttached(shown, expanded: false);
                var expanded = Assert.IsType<PublicIpAddress>(Assert.Single(cli.RunCli([.. ShowArgs(subscriptionId, groupName), "--expand", "ipConfiguration"])));
                AssertAttached(expanded, expanded: true);
                recording.Save();
            }
            finally
            {
                if (isRecording)
                {
                    // After the save, so the delete never lands in the recording.
                    try { cli.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
                }
            }
        }

        // Record mode captures one surface; playback also replays the reads through the real cmdlet.
        if (isRecording)
        {
            return;
        }

        // Each interceptor replays independently, so the PowerShell reads would otherwise match the
        // read the public IP create made before the NIC held it; start after the attach instead.
        var attached = recording.Record.Entries.FindLastIndex(e => e.Request.Uri.Contains($"/networkInterfaces/{AttachedNicName}", StringComparison.OrdinalIgnoreCase)) + 1;
        using var ps = new ScenarioHost(
            ClientType.PowerShell,
            new RecordPlaybackInterceptor(RecorderMode.Playback, recording.Record, alreadyConsumed: Enumerable.Range(0, attached)),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]);
        var sessionState = InitialSessionState.CreateDefault2();
        sessionState.Commands.Add(new SessionStateCmdletEntry("Get-AzPublicIpAddress", typeof(GetPublicIpAddressCmdlet), null));
        using var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();
        var identity = new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = AttachedGroupName,
            ["Name"] = PipName,
        };

        var pip = Assert.IsType<PublicIpAddress>(Assert.Single(Invoke(runspace, "Get-AzPublicIpAddress", identity)));
        AssertAttached(pip, expanded: false);
        var rendered = FormatList(runspace, pip);
        Assert.Matches($@"IpAddress\s+: {Regex.Escape(pip.IpAddress!)}", rendered);
        Assert.Contains($"/networkInterfaces/{AttachedNicName}/ipConfigurations/ipconfig1", rendered);

        var expandedPip = Assert.IsType<PublicIpAddress>(Assert.Single(Invoke(runspace, "Get-AzPublicIpAddress", new(identity) { ["ExpandResource"] = "ipConfiguration" })));
        AssertAttached(expandedPip, expanded: true);
        var expandedRendered = FormatList(runspace, expandedPip.IpConfiguration!);
        Assert.Matches($@"PrivateIpAddress\s+: {Regex.Escape(expandedPip.IpConfiguration!.PrivateIpAddress!)}", expandedRendered);
    }

    private static void AssertAttached(PublicIpAddress pip, bool expanded)
    {
        Assert.Equal(PipName, pip.Name);
        Assert.Equal("Succeeded", pip.ProvisioningState);
        Assert.Equal("Static", pip.PublicIpAllocationMethod);
        Assert.False(string.IsNullOrEmpty(pip.IpAddress));
        Assert.EndsWith($"/networkInterfaces/{AttachedNicName}/ipConfigurations/ipconfig1", pip.IpConfiguration?.Id);
        if (expanded)
        {
            Assert.Equal("ipconfig1", pip.IpConfiguration!.Name);
            Assert.False(string.IsNullOrEmpty(pip.IpConfiguration.PrivateIpAddress));
            Assert.EndsWith("/subnets/default", pip.IpConfiguration.Subnet?.Id);
            Assert.Equal(pip.Id, pip.IpConfiguration.PublicIpAddress?.Id, ignoreCase: true);
        }
        else
        {
            Assert.Null(pip.IpConfiguration!.PrivateIpAddress);
        }
    }

    /// <summary>What <c>| Format-List</c> prints; a property whose getter throws renders blank, as #1117 reported.</summary>
    private static string FormatList(Runspace runspace, object value)
    {
        using var powerShell = PowerShell.Create();
        powerShell.Runspace = runspace;
        var output = powerShell.AddCommand("Format-List").AddParameter("InputObject", value).AddCommand("Out-String").Invoke<string>();

        Assert.Empty(powerShell.Streams.Error);
        return Assert.Single(output);
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
            new FakePublicIpArmBackend(SubscriptionId, ResourceGroupName),
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

    private static string[] CreateArgs(string subscriptionId, string? groupName = null, string dnsLabel = DnsLabel) =>
    [
        "network", "public-ip", "create",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName ?? ResourceGroupName,
        "--name", PipName,
        "--location", Location,
        "--sku", "Standard",
        "--idle-timeout", "10",
        "--dns-name", dnsLabel,
        "--zone", "1", "2", "3",
        "--ip-tags", "RoutingPreference=Internet",
        "--tags", "env=dev", "temporary", "env=scenario",
    ];

    private static string[] ShowArgs(string subscriptionId, string? groupName = null) =>
    [
        "network", "public-ip", "show",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName ?? ResourceGroupName,
        "--name", PipName,
    ];

    private static string[] ListArgs(string subscriptionId, string? groupName = null) =>
    [
        "network", "public-ip", "list",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName ?? ResourceGroupName,
    ];

    private static string[] DeleteArgs(string subscriptionId, string? groupName = null) =>
    [
        "network", "public-ip", "delete",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName ?? ResourceGroupName,
        "--name", PipName,
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
        ["Name"] = PipName,
        ["Location"] = Location,
        ["Sku"] = "Standard",
        ["IdleTimeoutInMinutes"] = 10,
        ["DomainNameLabel"] = DnsLabel,
        ["Zone"] = new List<string> { "1", "2", "3" },
        ["IpTag"] = new List<string> { "RoutingPreference=Internet" },
        ["Tag"] = new List<string> { "env=dev", "temporary", "env=scenario" },
    };

    private static Dictionary<string, object> PsShowParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = PipName,
    };

    private static Dictionary<string, object> PsListParameters(string subscriptionId = SubscriptionId, string groupName = ResourceGroupName) => new()
    {
        ["SubscriptionId"] = subscriptionId,
        ["ResourceGroupName"] = groupName,
    };

    private static void AssertCreated(IReadOnlyList<object> outputs, string dnsLabel = DnsLabel) => AssertPip(Assert.Single(outputs), dnsLabel);

    private static void AssertShown(IReadOnlyList<object> outputs, string dnsLabel = DnsLabel) => AssertPip(Assert.Single(outputs), dnsLabel);

    private static void AssertListed(IReadOnlyList<object> outputs, string dnsLabel = DnsLabel)
    {
        var addresses = Assert.IsType<List<PublicIpAddress>>(Assert.Single(outputs));
        AssertPip(Assert.Single(addresses), dnsLabel);
    }

    private static void AssertPip(object output, string dnsLabel)
    {
        var pip = Assert.IsType<PublicIpAddress>(output);
        Assert.Equal(PipName, pip.Name);
        Assert.Equal(Location, pip.Location);
        Assert.Equal("Succeeded", pip.ProvisioningState);
        Assert.False(string.IsNullOrEmpty(pip.IpAddress));
        Assert.Equal(["1", "2", "3"], pip.Zones ?? []);

        // A bare tag key carries an empty value, and the last of a repeated key wins.
        Assert.Equal("scenario", pip.Tags?["env"]);
        Assert.Equal("", pip.Tags?["temporary"]);

        // The legacy defaults the customization applies, echoed back by the service.
        Assert.Equal("Standard", pip.Sku?.Name);
        Assert.Equal("Static", pip.PublicIpAllocationMethod);
        Assert.Equal("IPv4", pip.PublicIpAddressVersion);
        Assert.Equal(10, pip.IdleTimeoutInMinutes);
        Assert.Equal(dnsLabel, pip.DnsSettings?.DomainNameLabel);

        var ipTag = Assert.Single(pip.IpTags ?? []);
        Assert.Equal("RoutingPreference", ipTag.IpTagType);
        Assert.Equal("Internet", ipTag.Tag);
    }
}
