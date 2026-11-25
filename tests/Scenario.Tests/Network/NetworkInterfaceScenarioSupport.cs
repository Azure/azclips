using System.Net;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>One surface run of a network interface case. <see cref="SubscriptionId"/> is known only in playback.</summary>
internal sealed record NetworkInterfaceRun(ScenarioHost Host, RecordPlaybackInterceptor Traffic, string ResourceGroup, string? SubscriptionId);

/// <summary>
/// Shared steps for the network interface scenarios (<c>network nic create/show/list/delete</c> and
/// <c>New-AzNetworkInterface</c>, <c>Get-AzNetworkInterface_Get</c>, <c>Get-AzNetworkInterface_List</c>,
/// <c>Remove-AzNetworkInterface</c>). Test spec: <c>network-network-interfaces-tests</c>.
/// </summary>
internal static class NetworkInterfaceScenario
{
    public const string NetworkInterfacesPath = "/providers/Microsoft.Network/networkInterfaces";
    public const string VnetName = "nicvnet";
    public const string SubnetName = "nicsubnet";
    public const string NicName = "nic1";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Network];

    /// <summary>
    /// Record: runs the CLI steps once against live Azure with a run-scoped group name, then saves.
    /// Playback: replays the recording through the CLI steps, then through the PowerShell steps.
    /// PowerShell has no subnet cmdlet, so the PowerShell run first replays the CLI subnet create
    /// (<paramref name="cliSubnet"/>) on the same recording and passes the subnet ID on.
    /// </summary>
    public static void RunCase(Type testClass, string testName, string resourceGroup,
        System.Action<NetworkInterfaceRun> cli,
        Func<NetworkInterfaceRun, Subnet> cliSubnet,
        System.Action<NetworkInterfaceRun, string> powerShell,
        RecordingSanitizers? sanitizers = null)
    {
        var recording = SessionRecordFiles.OpenSession(testClass, testName, sanitizers: sanitizers);

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(resourceGroup, recording.Mode);
            var traffic = recording.CreateInterceptor();
            using var host = new ScenarioHost(ClientType.CLI, traffic, useLiveAuth: true, extensions: s_extensions);
            try
            {
                cli(new NetworkInterfaceRun(host, traffic, groupName, null));
                recording.Save();
            }
            finally
            {
                // After the save, so this safety delete never lands in the recording.
                try { host.RunCli("group", "delete", "--name", groupName); } catch { }
            }
            return;
        }

        var cliTraffic = recording.CreateInterceptor();
        using (var host = new ScenarioHost(ClientType.CLI, cliTraffic, extensions: s_extensions))
        {
            cli(new NetworkInterfaceRun(host, cliTraffic, resourceGroup, RecordingSanitizers.PlaceholderGuid));
        }

        var psTraffic = recording.CreateInterceptor();
        string subnetId;
        using (var host = new ScenarioHost(ClientType.CLI, psTraffic, extensions: s_extensions))
        {
            subnetId = cliSubnet(new NetworkInterfaceRun(host, psTraffic, resourceGroup, RecordingSanitizers.PlaceholderGuid)).Id!;
        }

        using (var host = new ScenarioHost(ClientType.PowerShell, psTraffic, extensions: s_extensions))
        {
            powerShell(new NetworkInterfaceRun(host, psTraffic, resourceGroup, RecordingSanitizers.PlaceholderGuid), subnetId);
        }
    }

    public static IReadOnlyList<object> Cli(NetworkInterfaceRun run, params string[] args) =>
        run.Host.RunCli(["network", .. args]);

    public static IReadOnlyList<object> Ps(NetworkInterfaceRun run, string command, Dictionary<string, object> parameters) =>
        run.Host.RunPowerShell(command, parameters, extension: "Network");

    public static void CliGroupCreate(NetworkInterfaceRun run, string location) =>
        run.Host.RunCli("group", "create", "--name", run.ResourceGroup, "--location", location);

    public static void CliGroupDelete(NetworkInterfaceRun run) => run.Host.RunCli("group", "delete", "--name", run.ResourceGroup);

    public static void PsGroupCreate(NetworkInterfaceRun run, string location) =>
        run.Host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object> { ["Name"] = run.ResourceGroup, ["Location"] = location }, extension: "Resources");

    public static void PsGroupDelete(NetworkInterfaceRun run) =>
        run.Host.RunPowerShell("Remove-AzResourceGroup", new Dictionary<string, object> { ["Name"] = run.ResourceGroup }, extension: "Resources");

    public static void CliVnetCreate(NetworkInterfaceRun run, string location, params string[] extra) =>
        Cli(run, ["vnet", "create", "-g", run.ResourceGroup, "-n", VnetName, "-l", location, "--address-prefixes", "10.0.0.0/16", .. extra]);

    public static Subnet CliSubnetCreate(NetworkInterfaceRun run, string prefix) =>
        Assert.IsType<Subnet>(Assert.Single(Cli(run, "vnet", "subnet", "create", "-g", run.ResourceGroup, "--vnet-name", VnetName,
            "-n", SubnetName, "--address-prefixes", prefix)));

    /// <summary>Sends the same vnet PUT as <see cref="CliVnetCreate"/>; the subnet comes from the CLI subnet create.</summary>
    public static void PsVnetCreate(NetworkInterfaceRun run, string location, string? edgeZone = null)
    {
        var parameters = new Dictionary<string, object>
        {
            ["ResourceGroupName"] = run.ResourceGroup,
            ["Name"] = VnetName,
            ["Location"] = location,
            ["AddressPrefix"] = new[] { "10.0.0.0/16" },
        };
        if (edgeZone is not null)
        {
            parameters["EdgeZone"] = edgeZone;
        }
        Ps(run, "New-AzVirtualNetwork", parameters);
    }

    public static NetworkInterface CliShow(NetworkInterfaceRun run, params string[] extra) =>
        Single(Cli(run, ["nic", "show", "-g", run.ResourceGroup, "-n", NicName, .. extra]));

    public static NetworkInterface PsShow(NetworkInterfaceRun run, string? expand = null)
    {
        var parameters = new Dictionary<string, object> { ["ResourceGroupName"] = run.ResourceGroup, ["Name"] = NicName };
        if (expand is not null)
        {
            parameters["ExpandResource"] = expand;
        }
        return Single(Ps(run, "Get-AzNetworkInterface_Get", parameters));
    }

    public static NetworkInterface Single(IReadOnlyList<object> outputs) => Assert.IsType<NetworkInterface>(Assert.Single(outputs));

    /// <summary>List output arrives either item by item or as one page collection; flatten both.</summary>
    public static List<NetworkInterface> Items(IReadOnlyList<object> outputs) =>
        [.. outputs.SelectMany(output => output switch
        {
            NetworkInterface nic => [nic],
            IEnumerable<NetworkInterface> page => page,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected list output {output.GetType()}."),
        })];

    /// <summary>Body of the PUT to the network interface that this step sent.</summary>
    public static JsonNode PutBody(NetworkInterfaceRun run, int sentBefore)
    {
        var put = run.Traffic.Sent.Skip(sentBefore).FirstOrDefault(request => request.Method == "PUT"
            && new Uri(request.Uri).AbsolutePath.EndsWith($"{NetworkInterfacesPath}/{NicName}", StringComparison.OrdinalIgnoreCase));
        Assert.True(put is not null, $"No PUT was sent to network interface '{NicName}'.");
        return JsonNode.Parse(put!.Body!)!;
    }

    /// <summary>The URI of the first GET this step sent; later GETs are next-page links.</summary>
    public static Uri FirstGetUri(NetworkInterfaceRun run, int sentBefore) =>
        new(run.Traffic.Sent.Skip(sentBefore).First(request => request.Method == "GET").Uri);

    public static string NicPath(NetworkInterfaceRun run) => $"/resourceGroups/{run.ResourceGroup}{NetworkInterfacesPath}/{NicName}";

    public static string NetworkResourceIdSuffix(NetworkInterfaceRun run, string resourceType, string name) =>
        $"/resourceGroups/{run.ResourceGroup}/providers/Microsoft.Network/{resourceType}/{name}";

    public static void AssertDeleteSent(NetworkInterfaceRun run, int sentBefore) =>
        Assert.Contains(run.Traffic.Sent.Skip(sentBefore), request => request.Method == "DELETE"
            && new Uri(request.Uri).AbsolutePath.EndsWith(NicPath(run), StringComparison.OrdinalIgnoreCase));

    /// <summary>A show of a deleted network interface fails with ResourceNotFound (HTTP 404).</summary>
    public static void AssertNotFound(System.Action show)
    {
        var error = Assert.ThrowsAny<Exception>(show);
        bool notFound = false;
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            notFound |= current is HttpRequestException { StatusCode: HttpStatusCode.NotFound };
        }
        Assert.True(notFound || error.ToString().Contains("ResourceNotFound", StringComparison.OrdinalIgnoreCase),
            $"Expected a not-found error, got: {error}");
    }
}
