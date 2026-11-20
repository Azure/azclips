using System.Text.Json.Nodes;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Creates a network interface in an Azure Extended Zone against live Azure. Recorded from the
/// CLI and replayed on both surfaces from the one recording, matching request bodies, so the
/// PowerShell surface must send the same extendedLocation the CLI sent. Kept apart from the
/// lifecycle scenario because edge-zone access depends on the recording subscription. The
/// recording subscription has no edge-zone access, so the service rejects the create.
/// </summary>
[Collection("Scenario Tests")]
public class NetworkInterfaceEdgeZoneScenarioTests
{
    private const string PlaybackSubscriptionId = RecordingSanitizers.PlaceholderGuid;
    private const string Location = "westus";
    private const string EdgeZone = "microsoftlosangeles1";

    private const string GroupBase = "scenario-nic-edge-rg";
    private const string VNetBase = "scenario-nic-edge-vnet";
    private const string SubnetBase = "scenario-nic-edge-subnet";
    private const string NicBase = "scenario-nic-edge";

    /// <summary>
    /// Case T2, verifying test spec scenario "T2 Network interface in an edge zone": the edge zone
    /// is sent as the interface's extendedLocation, and the service error is returned.
    /// </summary>
    [Fact]
    public void NetworkInterfaceInEdgeZone()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(NetworkInterfaceEdgeZoneScenarioTests),
            nameof(NetworkInterfaceInEdgeZone),
            new DefaultRequestMatcher(matchBody: true));
        var names = EdgeZoneNames.For(recording.Mode);

        if (recording.Mode == RecorderMode.Record)
        {
            var interceptor = recording.CreateInterceptor();
            using var host = new ScenarioHost(
                ClientType.CLI,
                interceptor,
                useLiveAuth: true,
                extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]);

            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                RunScenario(step => host.RunCli(step.Cli), interceptor, subscriptionId, names);
                recording.Save();
            }
            finally
            {
                // After the save, so the delete never lands in the recording.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", names.Group); } catch { }
            }
            return;
        }

        var cliInterceptor = recording.CreateInterceptor();
        using (var cli = new ScenarioHost(
            ClientType.CLI,
            cliInterceptor,
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]))
        {
            RunScenario(step => cli.RunCli(step.Cli), cliInterceptor, PlaybackSubscriptionId, names);
        }

        var psInterceptor = recording.CreateInterceptor();
        using (var ps = new ScenarioHost(
            ClientType.PowerShell,
            psInterceptor,
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]))
        {
            RunScenario(step => ps.RunPowerShell(step.PsCommand, step.Ps, extension: step.PsExtension), psInterceptor, PlaybackSubscriptionId, names);
        }
    }

    /// <summary>The T2 steps, run on one surface. Every step carries both its CLI and PowerShell form.</summary>
    private static void RunScenario(Func<Step, IReadOnlyList<object>> run, RecordPlaybackInterceptor interceptor, string subscriptionId, EdgeZoneNames n)
    {
        // Steps 1 and 2: setup.
        run(new Step(
            ["group", "create", "--subscription-id", subscriptionId, "--name", n.Group, "--location", Location],
            "New-AzResourceGroup",
            new() { ["SubscriptionId"] = subscriptionId, ["Name"] = n.Group, ["Location"] = Location },
            PsExtension: "Resources"));
        Assert.IsType<VirtualNetwork>(Assert.Single(run(new Step(
            ["network", "vnet", "create", "--subscription-id", subscriptionId, "--name", n.VNet, "--resource-group", n.Group, "--location", Location,
             "--subnet-name", n.Subnet, "--subnet-prefixes", "10.0.0.0/24"],
            "New-AzVirtualNetwork",
            new()
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = n.VNet,
                ["ResourceGroupName"] = n.Group,
                ["Location"] = Location,
                ["SubnetName"] = n.Subnet,
                ["SubnetPrefixes"] = new List<string> { "10.0.0.0/24" },
            }))));

        // Step 3: the edge zone goes on the wire as the interface's extendedLocation.
        int before = interceptor.Sent.Count;
        var rejected = Assert.ThrowsAny<Exception>(() => run(new Step(
            ["network", "nic", "create", "--subscription-id", subscriptionId, "--name", n.Nic, "--resource-group", n.Group, "--location", Location,
             "--edge-zone", EdgeZone, "--subnet", n.Subnet, "--vnet-name", n.VNet],
            "New-AzNetworkInterface",
            new()
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = n.Nic,
                ["ResourceGroupName"] = n.Group,
                ["Location"] = Location,
                ["EdgeZone"] = EdgeZone,
                ["Subnet"] = n.Subnet,
                ["VirtualNetworkName"] = n.VNet,
            })));
        Assert.Contains("UnsupportedEdgeZone", rejected.ToString());
        var put = Assert.Single(
            interceptor.Sent.Skip(before),
            request => request.Method == "PUT" && request.Uri.Contains($"/networkInterfaces/{n.Nic}?", StringComparison.OrdinalIgnoreCase));
        var extendedLocation = JsonNode.Parse(put.Body ?? "{}")?["extendedLocation"];
        Assert.NotNull(extendedLocation);
        Assert.Equal(EdgeZone, extendedLocation["name"]?.GetValue<string>());
        Assert.Equal("EdgeZone", extendedLocation["type"]?.GetValue<string>());
    }

    /// <summary>One step in its CLI form and its PowerShell form.</summary>
    private sealed record Step(string[] Cli, string PsCommand, Dictionary<string, object> Ps, string PsExtension = "Network");

    /// <summary>The T2 resource names, run-scoped in record mode.</summary>
    private sealed record EdgeZoneNames(string Group, string VNet, string Subnet, string Nic)
    {
        public static EdgeZoneNames For(RecorderMode mode) => new(
            TestRun.ScopedName(GroupBase, mode),
            TestRun.ScopedName(VNetBase, mode),
            TestRun.ScopedName(SubnetBase, mode),
            TestRun.ScopedName(NicBase, mode));
    }
}
