// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Network;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises the network security group lifecycle through the real dispatcher/binder and ARM
/// request pipeline on both client surfaces. Record mode runs the CLI steps against live Azure in a
/// run-scoped resource group; playback replays that one recording through the CLI, then through
/// PowerShell.
/// <para>
/// Playback matches request bodies as well as URIs, so the PowerShell steps are held to the exact
/// bodies the CLI recorded: the hashtable <c>-Tag</c> must serialize like <c>--tags foo=doo</c>,
/// and the create body must carry only <c>location</c> and <c>tags</c>. Subscription-wide list
/// responses keep only the scenario's resource group before they are saved.
/// </para>
/// <para>
/// The NSG steps that check PowerShell binding run the real cmdlet classes in a runspace: the
/// hashtable tag conversion, the default List parameter set, the Get set chosen by name, and
/// <c>-Force</c>/<c>-PassThru</c> on remove.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class NetworkSecurityGroupScenarioTests
{
    // Playback matches the placeholder the sanitisers wrote into the recording.
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-nsg-rg";
    private const string Location = "westus2";
    private const string VNetName = "scenario-nsg-vnet";
    private const string SubnetName = "scenario-nsg-subnet";
    private const string NicName = "scenario-nsg-nic";
    private const string Nsg1 = "test-nsg1";
    private const string Nsg2 = "test-nsg2";
    private const string NsgType = "Microsoft.Network/networkSecurityGroups";
    private const string ApiVersion = "api-version=2026-03-01";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Network];

    private static readonly string[] s_defaultRuleNames =
    [
        "AllowVnetInBound", "AllowAzureLoadBalancerInBound", "DenyAllInBound",
        "AllowVnetOutBound", "AllowInternetOutBound", "DenyAllOutBound",
    ];

    // T1: verifies "T1 Network security group lifecycle: default and explicit location, tags, show,
    // list by group and subscription, attached references, delete" of network-network-security-groups-tests.
    [Fact]
    public void NetworkSecurityGroupLifecycle()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(NetworkSecurityGroupScenarioTests),
            nameof(NetworkSecurityGroupLifecycle),
            new DefaultRequestMatcher(matchBody: true),
            RecordingSanitizers.Default.With([new ResourceGroupListSanitizer(ResourceGroupName)]));

        if (recording.Mode == RecorderMode.Record)
        {
            // Live ARM, CLI surface only; the subscription comes from the signed-in profile. The group
            // name carries a run-scoped suffix, stripped by the sanitisers before the file is written.
            var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
            var interceptor = recording.CreateInterceptor();
            using var host = new ScenarioHost(ClientType.CLI, interceptor, useLiveAuth: true, extensions: s_extensions);
            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                RunCliSteps(host, interceptor, subscriptionId, groupName);
                recording.Save();
            }
            finally
            {
                // After the save, so the delete never lands in the recording.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
            }
            return;
        }

        var cliInterceptor = recording.CreateInterceptor();
        using (var cli = new ScenarioHost(ClientType.CLI, cliInterceptor, extensions: s_extensions))
        {
            RunCliSteps(cli, cliInterceptor, SubscriptionId, ResourceGroupName);
        }

        var psInterceptor = recording.CreateInterceptor();
        using (var ps = new ScenarioHost(ClientType.PowerShell, psInterceptor, extensions: s_extensions))
        {
            RunPowerShellSteps(ps, psInterceptor, SubscriptionId, ResourceGroupName);
        }
    }

    private static void RunCliSteps(ScenarioHost cli, RecordPlaybackInterceptor requests, string subscriptionId, string groupName)
    {
        // 1-2: the resource group and the virtual network the subnet is created in.
        cli.RunCli("group", "create", "--subscription-id", subscriptionId, "--name", groupName, "--location", Location);
        cli.RunCli(
            "network", "vnet", "create", "--subscription-id", subscriptionId,
            "--name", VNetName, "--resource-group", groupName, "--location", Location,
            "--address-prefixes", "10.0.0.0/16");

        // 3: no location, so it is read from the resource group; tags only.
        var (created, sent) = Step(requests, () => cli.RunCli(
            "network", "nsg", "create", "--subscription-id", subscriptionId,
            "--name", Nsg1, "--resource-group", groupName, "--tags", "foo=doo"));
        AssertCreatedWithGroupLocation(Assert.Single(created), sent, subscriptionId, groupName);

        // 4: no resource group, so the subscription-wide list.
        (var listed, sent) = Step(requests, () => cli.RunCli("network", "nsg", "list", "--subscription-id", subscriptionId));
        AssertListRequest(sent, subscriptionId, groupName: null);
        AssertSubscriptionListIncludesNsg1(CliList(listed));

        // 5
        (listed, sent) = Step(requests, () => cli.RunCli(
            "network", "nsg", "list", "--subscription-id", subscriptionId, "--resource-group", groupName));
        AssertListRequest(sent, subscriptionId, groupName);
        AssertGroupListHasOnlyNsg1(CliList(listed));

        // 6
        AssertShownNsg1(Assert.Single(cli.RunCli(ShowArgs(subscriptionId, groupName, Nsg1))), groupName);

        // 7: explicit location, so no resource-group read.
        (created, sent) = Step(requests, () => cli.RunCli(
            "network", "nsg", "create", "--subscription-id", subscriptionId,
            "--name", Nsg2, "--resource-group", groupName, "--location", Location));
        AssertCreatedWithExplicitLocation(Assert.Single(created), sent, subscriptionId, groupName);

        // 8
        AssertUnattached(Assert.Single(cli.RunCli(ShowArgs(subscriptionId, groupName, Nsg2))));

        // 9-10: a subnet attached to test-nsg2 shows up on it.
        cli.RunCli(
            "network", "vnet", "subnet", "create", "--subscription-id", subscriptionId,
            "--name", SubnetName, "--vnet-name", VNetName, "--resource-group", groupName,
            "--address-prefixes", "10.0.1.0/24", "--network-security-group", Nsg2);
        AssertSubnetAttached(Assert.Single(cli.RunCli(ShowArgs(subscriptionId, groupName, Nsg2))));

        // 11-12: so does a network interface.
        cli.RunCli(
            "network", "nic", "create", "--subscription-id", subscriptionId,
            "--name", NicName, "--resource-group", groupName, "--location", Location,
            "--subnet", SubnetName, "--vnet-name", VNetName, "--network-security-group", Nsg2);
        AssertNicAttached(Assert.Single(cli.RunCli(ShowArgs(subscriptionId, groupName, Nsg2))));

        // 13-14: detach both before the NSGs are deleted.
        cli.RunCli("network", "nic", "delete", "--subscription-id", subscriptionId, "--name", NicName, "--resource-group", groupName);
        cli.RunCli("network", "vnet", "delete", "--subscription-id", subscriptionId, "--name", VNetName, "--resource-group", groupName);

        // 15-16: the delete finishes on the network RP's empty operation result, so it prints nothing.
        Assert.Empty(cli.RunCli(DeleteArgs(subscriptionId, groupName, Nsg1)));
        Assert.Empty(cli.RunCli(DeleteArgs(subscriptionId, groupName, Nsg2)));

        // 17
        Assert.Empty(CliList(cli.RunCli("network", "nsg", "list", "--subscription-id", subscriptionId, "--resource-group", groupName)));

        // 18
        AssertNoneInGroup(CliList(cli.RunCli("network", "nsg", "list", "--subscription-id", subscriptionId)), groupName);
    }

    private static void RunPowerShellSteps(ScenarioHost ps, RecordPlaybackInterceptor requests, string subscriptionId, string groupName)
    {
        // 1-2: the same setup requests and bodies as the CLI steps.
        ps.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
        {
            ["SubscriptionId"] = subscriptionId,
            ["Name"] = groupName,
            ["Location"] = Location,
        }, extension: "Resources");
        ps.RunPowerShell("New-AzVirtualNetwork", new Dictionary<string, object>
        {
            ["SubscriptionId"] = subscriptionId,
            ["Name"] = VNetName,
            ["ResourceGroupName"] = groupName,
            ["Location"] = Location,
            ["AddressPrefixes"] = new List<string> { "10.0.0.0/16" },
        }, extension: "Network");

        // 3: the real cmdlet, so the hashtable tag is converted before it reaches the body.
        var (created, sent) = Step(requests, () => ps.RunPowerShellCmdlet<NewNetworkSecurityGroupCmdlet>(new Dictionary<string, object>
        {
            ["SubscriptionId"] = subscriptionId,
            ["Name"] = Nsg1,
            ["ResourceGroupName"] = groupName,
            ["Tag"] = new Hashtable { ["foo"] = "doo" },
        }));
        AssertCreatedWithGroupLocation(Assert.Single(created), sent, subscriptionId, groupName);

        // 4: no ResourceGroupName, so the default List set; the cmdlet enumerates into individual objects.
        (var listed, sent) = Step(requests, () => ps.RunPowerShellCmdlet<GetNetworkSecurityGroupCmdlet>(
            PsParameters(subscriptionId)));
        AssertListRequest(sent, subscriptionId, groupName: null);
        AssertSubscriptionListIncludesNsg1(PsList(listed));

        // 5
        (listed, sent) = Step(requests, () => ps.RunPowerShellCmdlet<GetNetworkSecurityGroupCmdlet>(
            PsParameters(subscriptionId, groupName)));
        AssertListRequest(sent, subscriptionId, groupName);
        AssertGroupListHasOnlyNsg1(PsList(listed));

        // 6: Name chooses the Get set.
        AssertShownNsg1(Assert.Single(ps.RunPowerShellCmdlet<GetNetworkSecurityGroupCmdlet>(
            PsParameters(subscriptionId, groupName, Nsg1))), groupName);

        // 7
        (created, sent) = Step(requests, () => ps.RunPowerShell("New-AzNetworkSecurityGroup",
            new Dictionary<string, object>(PsParameters(subscriptionId, groupName, Nsg2)) { ["Location"] = Location },
            extension: "Network"));
        AssertCreatedWithExplicitLocation(Assert.Single(created), sent, subscriptionId, groupName);

        // 8
        AssertUnattached(Assert.Single(ps.RunPowerShell("Get-AzNetworkSecurityGroup_Get", PsParameters(subscriptionId, groupName, Nsg2), extension: "Network")));

        // 9-10
        ps.RunPowerShell("New-AzVirtualNetworkSubnet", new Dictionary<string, object>
        {
            ["SubscriptionId"] = subscriptionId,
            ["Name"] = SubnetName,
            ["VirtualNetworkName"] = VNetName,
            ["ResourceGroupName"] = groupName,
            ["AddressPrefixes"] = new List<string> { "10.0.1.0/24" },
            ["NetworkSecurityGroup"] = Nsg2,
        }, extension: "Network");
        AssertSubnetAttached(Assert.Single(ps.RunPowerShell("Get-AzNetworkSecurityGroup_Get", PsParameters(subscriptionId, groupName, Nsg2), extension: "Network")));

        // 11-12
        ps.RunPowerShell("New-AzNetworkInterface", new Dictionary<string, object>
        {
            ["SubscriptionId"] = subscriptionId,
            ["Name"] = NicName,
            ["ResourceGroupName"] = groupName,
            ["Location"] = Location,
            ["Subnet"] = SubnetName,
            ["VirtualNetworkName"] = VNetName,
            ["NetworkSecurityGroup"] = Nsg2,
        }, extension: "Network");
        AssertNicAttached(Assert.Single(ps.RunPowerShell("Get-AzNetworkSecurityGroup_Get", PsParameters(subscriptionId, groupName, Nsg2), extension: "Network")));

        // 13-14
        ps.RunPowerShell("Remove-AzNetworkInterface", PsParameters(subscriptionId, groupName, NicName), extension: "Network");
        ps.RunPowerShell("Remove-AzVirtualNetwork", PsParameters(subscriptionId, groupName, VNetName), extension: "Network");

        // 15: -Force skips the prompt a host without UI cannot answer; without -PassThru nothing is written.
        Assert.Empty(ps.RunPowerShellCmdlet<RemoveNetworkSecurityGroupCmdlet>(
            new Dictionary<string, object>(PsParameters(subscriptionId, groupName, Nsg1)) { ["Force"] = true }));

        // 16: the delete itself writes nothing, so -PassThru's true is the only output.
        var removed = ps.RunPowerShellCmdlet<RemoveNetworkSecurityGroupCmdlet>(
            new Dictionary<string, object>(PsParameters(subscriptionId, groupName, Nsg2)) { ["Force"] = true, ["PassThru"] = true });
        Assert.True(Assert.IsType<bool>(Assert.Single(removed)));

        // 17
        Assert.Empty(ps.RunPowerShellCmdlet<GetNetworkSecurityGroupCmdlet>(PsParameters(subscriptionId, groupName)));

        // 18
        AssertNoneInGroup(PsList(ps.RunPowerShellCmdlet<GetNetworkSecurityGroupCmdlet>(PsParameters(subscriptionId))), groupName);
    }

    /// <summary>Runs one step and returns its output with the requests it sent, in send order.</summary>
    private static (IReadOnlyList<object> Outputs, IReadOnlyList<CapturedHttpRequest> Sent) Step(
        RecordPlaybackInterceptor requests, Func<IReadOnlyList<object>> run)
    {
        int start = requests.Sent.Count;
        var outputs = run();
        IReadOnlyList<CapturedHttpRequest> sent = [.. requests.Sent.Skip(start)];
        return (outputs, sent);
    }

    private static string[] ShowArgs(string subscriptionId, string groupName, string nsgName) =>
    [
        "network", "nsg", "show",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName,
        "--name", nsgName,
    ];

    private static string[] DeleteArgs(string subscriptionId, string groupName, string nsgName) =>
    [
        "network", "nsg", "delete",
        "--subscription-id", subscriptionId,
        "--resource-group", groupName,
        "--name", nsgName,
    ];

    private static Dictionary<string, object> PsParameters(string subscriptionId, string? groupName = null, string? name = null)
    {
        var parameters = new Dictionary<string, object> { ["SubscriptionId"] = subscriptionId };
        if (groupName is not null) parameters["ResourceGroupName"] = groupName;
        if (name is not null) parameters["Name"] = name;
        return parameters;
    }

    /// <summary>The CLI writes a list as one collection, empty or not.</summary>
    private static List<NetworkSecurityGroup> CliList(IReadOnlyList<object> outputs) =>
        [.. outputs.SelectMany(output => Assert.IsAssignableFrom<IEnumerable<NetworkSecurityGroup>>(output))];

    /// <summary>The cmdlet enumerates the list, so every pipeline object is one network security group.</summary>
    private static List<NetworkSecurityGroup> PsList(IReadOnlyList<object> outputs) =>
        [.. outputs.Select(output => Assert.IsType<NetworkSecurityGroup>(output))];

    // Step 3: one bare NSG in the resource group's location, after a read of that group.
    private static void AssertCreatedWithGroupLocation(object output, IReadOnlyList<CapturedHttpRequest> sent, string subscriptionId, string groupName)
    {
        var nsg = AssertNsg(output, Nsg1);
        Assert.Equal("Succeeded", nsg.ProvisioningState);
        Assert.Equal(Location, nsg.Location);
        AssertFooTag(nsg.Tags);

        int read = IndexOf(sent, request => IsResourceGroupRead(request, subscriptionId, groupName));
        int put = IndexOf(sent, request => IsNsgPut(request, Nsg1));
        Assert.True(read >= 0, $"no GET of resource group '{groupName}' was sent");
        Assert.True(put >= 0, $"no PUT of '{Nsg1}' was sent");
        Assert.True(read < put, "the resource-group GET must be sent before the NSG PUT");
        AssertPutBody(sent[put], subscriptionId, groupName, Nsg1, withTags: true);
    }

    // Step 7: an explicit location needs no resource-group read, and the body carries only it.
    private static void AssertCreatedWithExplicitLocation(object output, IReadOnlyList<CapturedHttpRequest> sent, string subscriptionId, string groupName)
    {
        var nsg = AssertNsg(output, Nsg2);
        Assert.Equal(Location, nsg.Location);
        Assert.Equal("Succeeded", nsg.ProvisioningState);
        Assert.True(nsg.Tags is null || nsg.Tags.Count == 0, "the NSG was created without tags");

        Assert.DoesNotContain(sent, request => IsResourceGroupRead(request, subscriptionId, groupName));
        AssertPutBody(Assert.Single(sent, request => IsNsgPut(request, Nsg2)), subscriptionId, groupName, Nsg2, withTags: false);
    }

    private static void AssertPutBody(CapturedHttpRequest put, string subscriptionId, string groupName, string nsgName, bool withTags)
    {
        Assert.EndsWith(
            $"/subscriptions/{subscriptionId}/resourceGroups/{groupName}/providers/Microsoft.Network/networkSecurityGroups/{nsgName}?{ApiVersion}",
            put.Uri,
            StringComparison.OrdinalIgnoreCase);

        // No id and no properties, so neither flushConnection nor securityRules is sent.
        using var body = JsonDocument.Parse(put.Body ?? "{}");
        string[] expectedKeys = withTags ? new[] { "location", "tags" } : new[] { "location" };
        Assert.Equal(expectedKeys, body.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(Location, body.RootElement.GetProperty("location").GetString());
        if (withTags)
        {
            var tag = Assert.Single(body.RootElement.GetProperty("tags").EnumerateObject());
            Assert.Equal("foo", tag.Name);
            Assert.Equal("doo", tag.Value.GetString());
        }
    }

    // Steps 4 and 5: the subscription list has no resourceGroups segment; the group list does.
    private static void AssertListRequest(IReadOnlyList<CapturedHttpRequest> sent, string subscriptionId, string? groupName)
    {
        Assert.NotEmpty(sent);
        Assert.All(sent, request => Assert.Equal("GET", request.Method));
        string scope = groupName is null
            ? $"/subscriptions/{subscriptionId}"
            : $"/subscriptions/{subscriptionId}/resourceGroups/{groupName}";
        Assert.EndsWith($"{scope}/providers/Microsoft.Network/networkSecurityGroups?{ApiVersion}", sent[0].Uri, StringComparison.OrdinalIgnoreCase);
        if (groupName is null)
        {
            Assert.DoesNotContain("/resourceGroups/", sent[0].Uri, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Step 4: other NSGs in the subscription may be listed too, so only membership is checked.
    private static void AssertSubscriptionListIncludesNsg1(IReadOnlyList<NetworkSecurityGroup> listed)
    {
        Assert.All(listed, nsg => Assert.Equal(NsgType, nsg.Type));
        Assert.Contains(listed, nsg => nsg.Name == Nsg1);
    }

    // Step 5
    private static void AssertGroupListHasOnlyNsg1(IReadOnlyList<NetworkSecurityGroup> listed) =>
        AssertNsg(Assert.Single(listed), Nsg1);

    // Step 6
    private static void AssertShownNsg1(object output, string groupName)
    {
        var nsg = AssertNsg(output, Nsg1);
        Assert.Equal(Location, nsg.Location);
        AssertFooTag(nsg.Tags);
        Assert.EndsWith(
            $"/resourceGroups/{groupName}/providers/Microsoft.Network/networkSecurityGroups/{Nsg1}",
            nsg.Id,
            StringComparison.OrdinalIgnoreCase);
        AssertServiceRulesOnly(nsg);
    }

    // Step 8
    private static void AssertUnattached(object output)
    {
        var nsg = AssertNsg(output, Nsg2);
        AssertServiceRulesOnly(nsg);
        Assert.Empty(nsg.Subnets ?? []);
        Assert.Empty(nsg.NetworkInterfaces ?? []);
    }

    // Step 10
    private static void AssertSubnetAttached(object output)
    {
        var subnet = Assert.Single(AssertNsg(output, Nsg2).Subnets ?? []);
        Assert.EndsWith($"/virtualNetworks/{VNetName}/subnets/{SubnetName}", subnet.Id, StringComparison.OrdinalIgnoreCase);
    }

    // Step 12
    private static void AssertNicAttached(object output)
    {
        var nic = Assert.Single(AssertNsg(output, Nsg2).NetworkInterfaces ?? []);
        Assert.EndsWith($"/networkInterfaces/{NicName}", nic.Id, StringComparison.OrdinalIgnoreCase);
    }

    // Step 18
    private static void AssertNoneInGroup(IReadOnlyList<NetworkSecurityGroup> listed, string groupName) =>
        Assert.DoesNotContain(listed, nsg => (nsg.Id ?? string.Empty).Contains($"/resourceGroups/{groupName}/", StringComparison.OrdinalIgnoreCase));

    private static NetworkSecurityGroup AssertNsg(object output, string name)
    {
        var nsg = Assert.IsType<NetworkSecurityGroup>(output);
        Assert.Equal(name, nsg.Name);
        Assert.Equal(NsgType, nsg.Type);
        return nsg;
    }

    private static void AssertFooTag(IDictionary<string, string>? tags)
    {
        Assert.NotNull(tags);
        var tag = Assert.Single(tags);
        Assert.Equal("foo", tag.Key);
        Assert.Equal("doo", tag.Value);
    }

    // No custom rules; the service adds its six default rules, compared without order.
    private static void AssertServiceRulesOnly(NetworkSecurityGroup nsg)
    {
        Assert.Empty(nsg.SecurityRules ?? []);
        Assert.Equal(
            s_defaultRuleNames.Order(StringComparer.Ordinal).ToArray(),
            (nsg.DefaultSecurityRules ?? []).Select(rule => rule.Name).Order(StringComparer.Ordinal).ToArray());
    }

    private static bool IsResourceGroupRead(CapturedHttpRequest request, string subscriptionId, string groupName) =>
        request.Method == "GET"
        && new Uri(request.Uri).AbsolutePath.EndsWith($"/subscriptions/{subscriptionId}/resourcegroups/{groupName}", StringComparison.OrdinalIgnoreCase);

    private static bool IsNsgPut(CapturedHttpRequest request, string nsgName) =>
        request.Method == "PUT"
        && new Uri(request.Uri).AbsolutePath.EndsWith($"/networkSecurityGroups/{nsgName}", StringComparison.OrdinalIgnoreCase);

    private static int IndexOf(IReadOnlyList<CapturedHttpRequest> sent, Func<CapturedHttpRequest, bool> match)
    {
        for (int i = 0; i < sent.Count; i++)
        {
            if (match(sent[i])) return i;
        }
        return -1;
    }
}
