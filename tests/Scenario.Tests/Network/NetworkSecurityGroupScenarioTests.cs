using System.Text.Json.Nodes;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// Network security group scenarios for <c>network nsg create/show/list/delete</c> and
/// <c>New-AzNetworkSecurityGroup</c>, <c>Get-AzNetworkSecurityGroup_Get</c>, <c>Get-AzNetworkSecurityGroup_List</c>,
/// <c>Remove-AzNetworkSecurityGroup</c>. Each case creates its own resource group and deletes it at the end.
/// Record mode runs the CLI steps once against live Azure; playback replays the one recording through
/// the CLI steps and then, for cases on both surfaces, the PowerShell steps, which send the same requests.
/// Test spec: <c>network-network-security-groups-tests</c>.
/// </summary>
[Collection("Scenario Tests")]
public class NetworkSecurityGroupScenarioTests
{
    private const string SubscriptionId = RecordingSanitizers.PlaceholderGuid;
    private const string NetworkSecurityGroupType = "Microsoft.Network/networkSecurityGroups";
    private const string NetworkSecurityGroupsPath = "/providers/Microsoft.Network/networkSecurityGroups";

    private static readonly string[] s_defaultRuleNames =
    [
        "AllowVnetInBound", "AllowAzureLoadBalancerInBound", "DenyAllInBound",
        "AllowVnetOutBound", "AllowInternetOutBound", "DenyAllOutBound",
    ];

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Network];

    /// <summary>One surface run of a case. <see cref="SubscriptionId"/> is known only in playback.</summary>
    private sealed record Run(ScenarioHost Host, RecordPlaybackInterceptor Traffic, string ResourceGroup, string? SubscriptionId);

    // T1, test spec scenario "T1 Network security group lifecycle: create, show, list, delete"
    // (requirement "List scope by resource group presence").
    [Fact]
    public void NetworkSecurityGroupLifecycleCreateShowListDelete()
    {
        const string resourceGroup = "scenario-nsg-rg";
        const string location = "westus";
        const string name = "test-nsg1";

        RunCase(
            nameof(NetworkSecurityGroupLifecycleCreateShowListDelete),
            resourceGroup,
            cli: run =>
            {
                CliGroupCreate(run, location);

                int sent = run.Traffic.Sent.Count;
                NetworkSecurityGroup created = Single(Cli(run, "nsg", "create", "--resource-group", run.ResourceGroup, "--name", name,
                    "--location", location, "--tags", "foo=doo"));
                AssertCreateRequests(run, sent, name, location);
                AssertCreated(created, name, location);

                NetworkSecurityGroup shown = Single(Cli(run, "nsg", "show", "--resource-group", run.ResourceGroup, "--name", name));
                AssertShown(run, shown, name);
                string id = shown.Id!;

                sent = run.Traffic.Sent.Count;
                AssertListedInGroup(Items(Cli(run, "nsg", "list", "--resource-group", run.ResourceGroup)), name);
                AssertGroupListPath(run, FirstGetPath(run, sent));

                sent = run.Traffic.Sent.Count;
                AssertListedInSubscription(Items(Cli(run, "nsg", "list")), id);
                AssertSubscriptionListPath(run, FirstGetPath(run, sent));

                sent = run.Traffic.Sent.Count;
                AssertDeleted(run, sent, Cli(run, "nsg", "delete", "--resource-group", run.ResourceGroup, "--name", name), name);

                Assert.Empty(Items(Cli(run, "nsg", "list", "--resource-group", run.ResourceGroup)));
                Assert.DoesNotContain(Items(Cli(run, "nsg", "list")), nsg => SameId(nsg.Id, id));

                CliGroupDelete(run);
            },
            powerShell: run =>
            {
                PsGroupCreate(run, location);

                int sent = run.Traffic.Sent.Count;
                NetworkSecurityGroup created = Single(Ps(run, "New-AzNetworkSecurityGroup", new()
                {
                    ["Name"] = name,
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Location"] = location,
                    ["Tag"] = new Dictionary<string, string> { ["foo"] = "doo" },
                }));
                AssertCreateRequests(run, sent, name, location);
                AssertCreated(created, name, location);

                NetworkSecurityGroup shown = Single(Ps(run, "Get-AzNetworkSecurityGroup_Get", new()
                {
                    ["ResourceGroupName"] = run.ResourceGroup,
                    ["Name"] = name,
                }));
                AssertShown(run, shown, name);
                string id = shown.Id!;

                sent = run.Traffic.Sent.Count;
                AssertListedInGroup(Items(Ps(run, "Get-AzNetworkSecurityGroup_List", new() { ["ResourceGroupName"] = run.ResourceGroup })), name);
                AssertGroupListPath(run, FirstGetPath(run, sent));

                sent = run.Traffic.Sent.Count;
                AssertListedInSubscription(Items(Ps(run, "Get-AzNetworkSecurityGroup_List", new())), id);
                AssertSubscriptionListPath(run, FirstGetPath(run, sent));

                sent = run.Traffic.Sent.Count;
                AssertDeleted(run, sent, Ps(run, "Remove-AzNetworkSecurityGroup", new() { ["ResourceGroupName"] = run.ResourceGroup, ["Name"] = name }), name);

                Assert.Empty(Items(Ps(run, "Get-AzNetworkSecurityGroup_List", new() { ["ResourceGroupName"] = run.ResourceGroup })));
                Assert.DoesNotContain(Items(Ps(run, "Get-AzNetworkSecurityGroup_List", new())), nsg => SameId(nsg.Id, id));

                PsGroupDelete(run);
            },
            // The subscription-wide list returns every network security group in the recording subscription;
            // keep only this case's group so nobody else's groups reach the committed recording.
            sanitizers: RecordingSanitizers.Default.With([new ResourceGroupListSanitizer(resourceGroup)]));

        static void AssertCreateRequests(Run run, int sentBefore, string name, string location)
        {
            var step = run.Traffic.Sent.Skip(sentBefore).ToList();
            int put = PutIndex(step, name);
            Assert.DoesNotContain(step.Take(put), request => IsResourceGroupGet(request, run.ResourceGroup));

            JsonNode body = JsonNode.Parse(step[put].Body!)!;
            Assert.Equal(location, body["location"]!.GetValue<string>());
            Assert.Equal("doo", body["tags"]!["foo"]!.GetValue<string>());
            Assert.Single(body["tags"]!.AsObject());
        }

        static void AssertCreated(NetworkSecurityGroup nsg, string name, string location)
        {
            Assert.Equal(name, nsg.Name);
            Assert.Equal(NetworkSecurityGroupType, nsg.Type);
            Assert.Equal(location, nsg.Location);
            Assert.Equal(new Dictionary<string, string> { ["foo"] = "doo" }, nsg.Tags!);
            Assert.Empty(nsg.SecurityRules ?? []);
            AssertDefaultRules(nsg);
        }

        static void AssertShown(Run run, NetworkSecurityGroup nsg, string name)
        {
            Assert.Equal(name, nsg.Name);
            Assert.Equal(NetworkSecurityGroupType, nsg.Type);
            Assert.EndsWith($"/resourceGroups/{run.ResourceGroup}{NetworkSecurityGroupsPath}/{name}", nsg.Id, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Succeeded", nsg.ProvisioningState);
            Assert.False(string.IsNullOrEmpty(nsg.ResourceGuid));
            Assert.Equal(s_defaultRuleNames.Length, nsg.DefaultSecurityRules!.Count);
        }

        static void AssertListedInGroup(IReadOnlyList<NetworkSecurityGroup> nsgs, string name)
        {
            var nsg = Assert.Single(nsgs);
            Assert.Equal(name, nsg.Name);
            Assert.Equal(NetworkSecurityGroupType, nsg.Type);
        }

        static void AssertListedInSubscription(IReadOnlyList<NetworkSecurityGroup> nsgs, string id)
        {
            Assert.All(nsgs, nsg => Assert.Equal(NetworkSecurityGroupType, nsg.Type));
            Assert.Contains(nsgs, nsg => SameId(nsg.Id, id));
        }

        static void AssertDeleted(Run run, int sentBefore, IReadOnlyList<object> outputs, string name)
        {
            Assert.Contains(run.Traffic.Sent.Skip(sentBefore), request => request.Method == "DELETE"
                && new Uri(request.Uri).AbsolutePath.EndsWith($"/resourceGroups/{run.ResourceGroup}{NetworkSecurityGroupsPath}/{name}", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(outputs, output => output is NetworkSecurityGroup);
        }
    }

    // T2, test spec scenario "T2 Create without location takes the resource group location"
    // (requirement "Default location from the resource group"). CLI only: PowerShell -Location is required.
    [Fact]
    public void CreateWithoutLocationTakesResourceGroupLocation()
    {
        const string location = "westus";
        const string name = "test-nsg1";

        RunCase(
            nameof(CreateWithoutLocationTakesResourceGroupLocation),
            "scenario-nsg-default-location-rg",
            cli: run =>
            {
                CliGroupCreate(run, location);

                int sent = run.Traffic.Sent.Count;
                NetworkSecurityGroup created = Single(Cli(run, "nsg", "create", "--resource-group", run.ResourceGroup, "--name", name));

                var step = run.Traffic.Sent.Skip(sent).ToList();
                int put = PutIndex(step, name);
                Assert.Contains(step.Take(put), request => IsResourceGroupGet(request, run.ResourceGroup));
                Assert.Equal(location, JsonNode.Parse(step[put].Body!)!["location"]!.GetValue<string>());

                Assert.Equal(name, created.Name);
                Assert.Equal(location, created.Location);

                Cli(run, "nsg", "delete", "--resource-group", run.ResourceGroup, "--name", name);
                CliGroupDelete(run);
            },
            powerShell: null);
    }

    /// <summary>
    /// Record: runs the CLI steps once against live Azure with a run-scoped group name, then saves.
    /// Playback: replays the recording through the CLI steps, then through the PowerShell steps when given.
    /// </summary>
    private static void RunCase(string testName, string resourceGroup, Action<Run> cli, Action<Run>? powerShell, RecordingSanitizers? sanitizers = null)
    {
        var recording = SessionRecordFiles.OpenSession(typeof(NetworkSecurityGroupScenarioTests), testName, sanitizers: sanitizers);

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(resourceGroup, recording.Mode);
            var traffic = recording.CreateInterceptor();
            using var host = new ScenarioHost(ClientType.CLI, traffic, useLiveAuth: true, extensions: s_extensions);
            try
            {
                cli(new Run(host, traffic, groupName, null));
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
            cli(new Run(host, cliTraffic, resourceGroup, SubscriptionId));
        }

        if (powerShell is null)
        {
            return;
        }

        var psTraffic = recording.CreateInterceptor();
        using (var host = new ScenarioHost(ClientType.PowerShell, psTraffic, extensions: s_extensions))
        {
            powerShell(new Run(host, psTraffic, resourceGroup, SubscriptionId));
        }
    }

    private static IReadOnlyList<object> Cli(Run run, params string[] args) => run.Host.RunCli(["network", .. args]);

    private static IReadOnlyList<object> Ps(Run run, string command, Dictionary<string, object> parameters) =>
        run.Host.RunPowerShell(command, parameters, extension: "Network");

    private static void CliGroupCreate(Run run, string location) =>
        run.Host.RunCli("group", "create", "--name", run.ResourceGroup, "--location", location);

    private static void CliGroupDelete(Run run) => run.Host.RunCli("group", "delete", "--name", run.ResourceGroup);

    private static void PsGroupCreate(Run run, string location) =>
        run.Host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object> { ["Name"] = run.ResourceGroup, ["Location"] = location }, extension: "Resources");

    private static void PsGroupDelete(Run run) =>
        run.Host.RunPowerShell("Remove-AzResourceGroup", new Dictionary<string, object> { ["Name"] = run.ResourceGroup }, extension: "Resources");

    private static NetworkSecurityGroup Single(IReadOnlyList<object> outputs) => Assert.IsType<NetworkSecurityGroup>(Assert.Single(outputs));

    /// <summary>List output arrives either item by item or as one page collection; flatten both.</summary>
    private static List<NetworkSecurityGroup> Items(IReadOnlyList<object> outputs) =>
        [.. outputs.SelectMany(output => output switch
        {
            NetworkSecurityGroup nsg => [nsg],
            IEnumerable<NetworkSecurityGroup> page => page,
            _ => throw new Xunit.Sdk.XunitException($"Unexpected list output {output.GetType()}."),
        })];

    private static void AssertDefaultRules(NetworkSecurityGroup nsg)
    {
        var names = nsg.DefaultSecurityRules!.Select(rule => rule.Name).Order(StringComparer.Ordinal);
        Assert.Equal(s_defaultRuleNames.Order(StringComparer.Ordinal), names);
    }

    private static bool SameId(string? left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>Index, in this step's requests, of the PUT to the named network security group.</summary>
    private static int PutIndex(List<CapturedHttpRequest> step, string name)
    {
        int index = step.FindIndex(request => request.Method == "PUT"
            && new Uri(request.Uri).AbsolutePath.EndsWith($"{NetworkSecurityGroupsPath}/{name}", StringComparison.OrdinalIgnoreCase));
        Assert.True(index >= 0, $"No PUT was sent to network security group '{name}'.");
        return index;
    }

    /// <summary>ResourceGroups_Get: GET /subscriptions/{id}/resourceGroups/{name}.</summary>
    private static bool IsResourceGroupGet(CapturedHttpRequest request, string resourceGroup) =>
        request.Method == "GET"
        && new Uri(request.Uri).AbsolutePath.TrimEnd('/').EndsWith($"/resourceGroups/{resourceGroup}", StringComparison.OrdinalIgnoreCase);

    /// <summary>The path of the first GET this step sent; later GETs are next-page links.</summary>
    private static string FirstGetPath(Run run, int sentBefore) =>
        new Uri(run.Traffic.Sent.Skip(sentBefore).First(request => request.Method == "GET").Uri).AbsolutePath;

    private static void AssertGroupListPath(Run run, string path) =>
        Assert.EndsWith($"/resourceGroups/{run.ResourceGroup}{NetworkSecurityGroupsPath}", path, StringComparison.OrdinalIgnoreCase);

    // The subscription is the developer's while recording, so its exact value is asserted only in playback.
    private static void AssertSubscriptionListPath(Run run, string path)
    {
        Assert.DoesNotContain("/resourceGroups/", path, StringComparison.OrdinalIgnoreCase);
        if (run.SubscriptionId is not null)
        {
            Assert.Equal($"/subscriptions/{run.SubscriptionId}{NetworkSecurityGroupsPath}", path, ignoreCase: true);
        }
        else
        {
            Assert.StartsWith("/subscriptions/", path, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(NetworkSecurityGroupsPath, path, StringComparison.OrdinalIgnoreCase);
        }
    }
}
