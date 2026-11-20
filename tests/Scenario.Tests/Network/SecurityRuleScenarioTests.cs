// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises the security rule commands of the SecurityRules operation group end to end on both
/// client surfaces: <c>network nsg rule create|update|show|list|delete</c> and
/// <c>New-AzNetworkSecurityRule</c>, <c>Set-AzNetworkSecurityRule</c>,
/// <c>Get-AzNetworkSecurityRule_Get</c>, <c>Get-AzNetworkSecurityRule_List</c>,
/// <c>Remove-AzNetworkSecurityRule</c>.
/// <para>
/// Record mode drives the CLI against live Azure; playback replays the one recording through the
/// CLI and then PowerShell with the same names and values. Playback matches on method and URI only,
/// so the request bodies each surface builds are checked on the interceptor's <c>Sent</c> list.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class SecurityRuleScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string Location = "westus";

    private const string ExtendedGroupName = "scenario-extended-nsg-rg";
    private const string ExtendedNsgName = "nsg1";
    private const string ExtendedRuleName = "rule1";

    private const string LifecycleGroupName = "scenario-nsg-rule-rg";
    private const string LifecycleNsgName = "test-nsg1";
    private const string LifecycleRuleName = "web";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Network];

    /// <summary>
    /// T1, verifies test spec scenario "T1 Rule address and port lists switch between singular and
    /// plural on create and update" (requirement "Security rule update replaces given address and
    /// port lists").
    /// </summary>
    [Fact]
    public void RuleAddressAndPortListsSwitchBetweenSingularAndPluralOnCreateAndUpdate()
        => RunScenario(nameof(RuleAddressAndPortListsSwitchBetweenSingularAndPluralOnCreateAndUpdate), ExtendedGroupName, RunListSwitchSteps);

    /// <summary>
    /// T2, verifies test spec scenario "T2 Security rule create, list, show, update, partial update
    /// and delete" (requirement "Security rule update applies only the given inputs").
    /// </summary>
    [Fact]
    public void SecurityRuleCreateListShowUpdatePartialUpdateAndDelete()
        => RunScenario(nameof(SecurityRuleCreateListShowUpdatePartialUpdateAndDelete), LifecycleGroupName, RunLifecycleSteps);

    /// <summary>
    /// Records the steps from the CLI, or replays them through the CLI and then PowerShell, each
    /// surface with its own interceptor so each replays the whole recording.
    /// </summary>
    private static void RunScenario(string testName, string baseGroupName, Action<RuleSurface> steps)
    {
        var recording = SessionRecordFiles.OpenSession(typeof(SecurityRuleScenarioTests), testName);

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(baseGroupName, recording.Mode);
            var interceptor = recording.CreateInterceptor();
            // useLiveAuth keeps the real profile and token; the stubs would 401 against real ARM.
            using var host = new ScenarioHost(ClientType.CLI, interceptor, useLiveAuth: true, extensions: s_extensions);
            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                steps(new RuleSurface(host, interceptor, ClientType.CLI, subscriptionId, groupName));
                recording.Save();
            }
            finally
            {
                // After the save, so the group delete never lands in the recording.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
            }
            return;
        }

        foreach (var clientType in new[] { ClientType.CLI, ClientType.PowerShell })
        {
            var interceptor = recording.CreateInterceptor();
            using var host = new ScenarioHost(clientType, interceptor, extensions: s_extensions);
            steps(new RuleSurface(host, interceptor, clientType, SubscriptionId, baseGroupName));
        }
    }

    // ── T1 ──

    private static void RunListSwitchSteps(RuleSurface surface)
    {
        // Steps 1-2: setup.
        surface.CreateGroup();
        surface.CreateNsg(ExtendedNsgName);

        // Step 3: plural destination prefixes and source ports, singular source prefix, defaults elsewhere.
        var created = surface.CreateRule(ExtendedNsgName, ExtendedRuleName, new RuleInputs
        {
            Priority = 1000,
            Access = "allow",
            Direction = "inbound",
            DestinationAddressPrefixes = ["10.0.0.0/24", "11.0.0.0/24"],
            SourceAddressPrefixes = ["*"],
            SourcePortRanges = ["700-900", "1000-1100"],
        });
        AssertListSwitchCreated(created);

        // Step 4: every list flips between its singular and plural property.
        var updated = surface.UpdateRule(ExtendedNsgName, ExtendedRuleName, new RuleInputs
        {
            DestinationAddressPrefixes = ["Internet"],
            SourceAddressPrefixes = ["10.0.0.0/24", "11.0.0.0/24"],
            SourcePortRanges = ["*"],
            DestinationPortRanges = ["500-1000", "2000", "3000"],
        });
        AssertListSwitchUpdated(updated);
    }

    private static void AssertListSwitchCreated(RuleStep step)
    {
        var body = PutBody(step, ExtendedNsgName, ExtendedRuleName);
        Assert.Equal(ExtendedRuleName, body.GetProperty("name").GetString());
        var properties = body.GetProperty("properties");
        AssertBodyStrings(properties, "destinationAddressPrefixes", "10.0.0.0/24", "11.0.0.0/24");
        AssertBodyAbsent(properties, "destinationAddressPrefix");
        AssertBodyString(properties, "sourceAddressPrefix", "*");
        AssertBodyAbsent(properties, "sourceAddressPrefixes");
        AssertBodyStrings(properties, "sourcePortRanges", "700-900", "1000-1100");
        AssertBodyAbsent(properties, "sourcePortRange");
        AssertBodyString(properties, "destinationPortRange", "80");
        AssertBodyAbsent(properties, "destinationPortRanges");
        AssertBodyString(properties, "protocol", "*");

        var rule = SingleRule(step);
        Assert.Equal(ExtendedRuleName, rule.Name);
        AssertSameItems(rule.DestinationAddressPrefixes, "10.0.0.0/24", "11.0.0.0/24");
        Assert.Null(rule.DestinationAddressPrefix);
        Assert.Equal("*", rule.SourceAddressPrefix);
        AssertNoItems(rule.SourceAddressPrefixes);
        AssertSameItems(rule.SourcePortRanges, "700-900", "1000-1100");
        Assert.Null(rule.SourcePortRange);
        Assert.Equal("80", rule.DestinationPortRange);
        AssertNoItems(rule.DestinationPortRanges);
    }

    private static void AssertListSwitchUpdated(RuleStep step)
    {
        AssertGetBeforePut(step, ExtendedNsgName, ExtendedRuleName);

        var properties = PutBody(step, ExtendedNsgName, ExtendedRuleName).GetProperty("properties");
        AssertBodyString(properties, "destinationAddressPrefix", "Internet");
        AssertBodyAbsent(properties, "destinationAddressPrefixes");
        AssertBodyStrings(properties, "sourceAddressPrefixes", "10.0.0.0/24", "11.0.0.0/24");
        AssertBodyAbsent(properties, "sourceAddressPrefix");
        AssertBodyString(properties, "sourcePortRange", "*");
        AssertBodyAbsent(properties, "sourcePortRanges");
        AssertBodyStrings(properties, "destinationPortRanges", "500-1000", "2000", "3000");
        AssertBodyAbsent(properties, "destinationPortRange");

        var rule = SingleRule(step);
        Assert.Equal("Internet", rule.DestinationAddressPrefix);
        AssertNoItems(rule.DestinationAddressPrefixes);
        AssertSameItems(rule.SourceAddressPrefixes, "10.0.0.0/24", "11.0.0.0/24");
        Assert.Null(rule.SourceAddressPrefix);
        Assert.Equal("*", rule.SourcePortRange);
        AssertNoItems(rule.SourcePortRanges);
        AssertSameItems(rule.DestinationPortRanges, "500-1000", "2000", "3000");
        Assert.Null(rule.DestinationPortRange);
        // Not given on the update, so it is kept from the existing rule.
        Assert.Equal(1000, rule.Priority);
    }

    // ── T2 ──

    private static void RunLifecycleSteps(RuleSurface surface)
    {
        // Steps 1-2: setup.
        surface.CreateGroup();
        surface.CreateNsg(LifecycleNsgName, tagKey: "foo", tagValue: "doo");

        // Step 3: every list has one item, so each goes on its singular property.
        AssertLifecycleCreated(surface.CreateRule(LifecycleNsgName, LifecycleRuleName, new RuleInputs
        {
            Priority = 1000,
            Access = "allow",
            Direction = "inbound",
            Protocol = "*",
            DestinationAddressPrefixes = ["1234"],
            SourceAddressPrefixes = ["789"],
            SourcePortRanges = ["*"],
            DestinationPortRanges = ["4444"],
        }));

        // Step 4.
        var listed = RuleList(surface.ListRules(LifecycleNsgName));
        Assert.Equal(LifecycleRuleName, Assert.Single(listed).Name);

        // Step 5.
        var shown = SingleRule(surface.ShowRule(LifecycleNsgName, LifecycleRuleName));
        Assert.Equal(LifecycleRuleName, shown.Name);
        Assert.Equal(1000, shown.Priority);
        Assert.Equal("4444", shown.DestinationPortRange);

        // Step 6: update every scalar and list.
        AssertLifecycleUpdated(surface.UpdateRule(LifecycleNsgName, LifecycleRuleName, new RuleInputs
        {
            Direction = "Outbound",
            Access = "DENY",
            DestinationAddressPrefixes = ["111"],
            Protocol = "Tcp",
            SourceAddressPrefixes = ["111"],
            SourcePortRanges = ["1234-1235"],
            DestinationPortRanges = ["1234-1235"],
            Priority = 888,
            Description = "greatrule",
        }));

        // Step 7: a partial update keeps everything it was not given, with no create defaults.
        AssertLifecyclePartiallyUpdated(surface.UpdateRule(LifecycleNsgName, LifecycleRuleName, new RuleInputs
        {
            Description = "cool",
        }));

        // Step 8.
        Assert.Empty(surface.DeleteRule(LifecycleNsgName, LifecycleRuleName).Outputs);

        // Step 9.
        Assert.Empty(RuleList(surface.ListRules(LifecycleNsgName)));

        // Step 10: cleanup.
        surface.DeleteNsg(LifecycleNsgName);
    }

    private static void AssertLifecycleCreated(RuleStep step)
    {
        var body = PutBody(step, LifecycleNsgName, LifecycleRuleName);
        Assert.Equal(LifecycleRuleName, body.GetProperty("name").GetString());
        var properties = body.GetProperty("properties");
        AssertBodyInt(properties, "priority", 1000);
        AssertBodyString(properties, "protocol", "*");
        AssertBodyString(properties, "access", "allow");
        AssertBodyString(properties, "direction", "inbound");
        AssertBodyString(properties, "sourceAddressPrefix", "789");
        AssertBodyString(properties, "destinationAddressPrefix", "1234");
        AssertBodyString(properties, "sourcePortRange", "*");
        AssertBodyString(properties, "destinationPortRange", "4444");
        AssertBodyAbsent(properties, "sourceAddressPrefixes");
        AssertBodyAbsent(properties, "destinationAddressPrefixes");
        AssertBodyAbsent(properties, "sourcePortRanges");
        AssertBodyAbsent(properties, "destinationPortRanges");

        var rule = SingleRule(step);
        Assert.Equal(LifecycleRuleName, rule.Name);
        Assert.Equal(1000, rule.Priority);
    }

    private static void AssertLifecycleUpdated(RuleStep step)
    {
        AssertGetBeforePut(step, LifecycleNsgName, LifecycleRuleName);

        var properties = PutBody(step, LifecycleNsgName, LifecycleRuleName).GetProperty("properties");
        AssertBodyString(properties, "access", "DENY");
        AssertBodyString(properties, "direction", "Outbound");
        AssertBodyString(properties, "protocol", "Tcp");
        AssertBodyInt(properties, "priority", 888);
        AssertBodyString(properties, "description", "greatrule");
        AssertBodyString(properties, "sourceAddressPrefix", "111");
        AssertBodyString(properties, "destinationAddressPrefix", "111");
        AssertBodyString(properties, "sourcePortRange", "1234-1235");
        AssertBodyString(properties, "destinationPortRange", "1234-1235");

        var rule = SingleRule(step);
        Assert.Equal("Outbound", rule.Direction);
        Assert.Equal("Tcp", rule.Protocol);
        Assert.Equal(888, rule.Priority);
        Assert.Equal("greatrule", rule.Description);
        Assert.Equal("111", rule.SourceAddressPrefix);
        Assert.Equal("111", rule.DestinationAddressPrefix);
        Assert.Equal("1234-1235", rule.SourcePortRange);
    }

    private static void AssertLifecyclePartiallyUpdated(RuleStep step)
    {
        AssertGetBeforePut(step, LifecycleNsgName, LifecycleRuleName);

        var properties = PutBody(step, LifecycleNsgName, LifecycleRuleName).GetProperty("properties");
        AssertBodyString(properties, "description", "cool");
        AssertBodyInt(properties, "priority", 888);
        AssertBodyString(properties, "direction", "Outbound");
        AssertBodyString(properties, "protocol", "Tcp");
        AssertBodyString(properties, "destinationPortRange", "1234-1235");

        var rule = SingleRule(step);
        Assert.Equal("cool", rule.Description);
        Assert.Equal(888, rule.Priority);
        Assert.Equal("Outbound", rule.Direction);
        Assert.Equal("Tcp", rule.Protocol);
        Assert.Equal("1234-1235", rule.SourcePortRange);
    }

    // ── Shared assertion helpers ──

    private static SecurityRule SingleRule(RuleStep step) => Assert.IsType<SecurityRule>(Assert.Single(step.Outputs));

    private static List<SecurityRule> RuleList(RuleStep step) => Assert.IsType<List<SecurityRule>>(Assert.Single(step.Outputs));

    /// <summary>Same items in any order: the service's ordering of a returned list is not asserted.</summary>
    private static void AssertSameItems(IList<string>? actual, params string[] expected)
        => Assert.Equal(expected.Order(StringComparer.Ordinal).ToArray(), (actual ?? []).Order(StringComparer.Ordinal).ToArray());

    /// <summary>The service returns <c>[]</c> for a plural property that was not given, so null and empty both mean "no items".</summary>
    private static void AssertNoItems(IList<string>? actual) => Assert.Empty(actual ?? []);

    private static bool IsRuleUri(string uri, string nsgName, string ruleName)
        => new Uri(uri).AbsolutePath.EndsWith($"/networkSecurityGroups/{nsgName}/securityRules/{ruleName}", StringComparison.OrdinalIgnoreCase);

    /// <summary>The body of the one PUT the step sent to the rule.</summary>
    private static JsonElement PutBody(RuleStep step, string nsgName, string ruleName)
    {
        var put = Assert.Single(step.Requests, request => request.Method == "PUT" && IsRuleUri(request.Uri, nsgName, ruleName));
        Assert.NotNull(put.Body);
        using var document = JsonDocument.Parse(put.Body);
        return document.RootElement.Clone();
    }

    /// <summary>Update reads the rule before it writes it: a GET of the same URL precedes the PUT.</summary>
    private static void AssertGetBeforePut(RuleStep step, string nsgName, string ruleName)
    {
        var requests = step.Requests.ToList();
        int put = requests.FindIndex(request => request.Method == "PUT" && IsRuleUri(request.Uri, nsgName, ruleName));
        Assert.True(put > 0, $"expected a GET of security rule '{ruleName}' before its PUT");
        string putUri = requests[put].Uri;
        Assert.Contains(requests.Take(put), request => request.Method == "GET" && string.Equals(request.Uri, putUri, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertBodyString(JsonElement properties, string name, string expected)
    {
        Assert.True(properties.TryGetProperty(name, out var value), $"the PUT body has no properties.{name}");
        Assert.Equal(expected, value.GetString());
    }

    private static void AssertBodyInt(JsonElement properties, string name, int expected)
    {
        Assert.True(properties.TryGetProperty(name, out var value), $"the PUT body has no properties.{name}");
        Assert.Equal(expected, value.GetInt32());
    }

    private static void AssertBodyStrings(JsonElement properties, string name, params string[] expected)
    {
        Assert.True(properties.TryGetProperty(name, out var value), $"the PUT body has no properties.{name}");
        Assert.Equal(expected, value.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray());
    }

    private static void AssertBodyAbsent(JsonElement properties, string name)
        => Assert.False(
            properties.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null,
            $"the PUT body unexpectedly has properties.{name}");

    // ── Surface driver ──

    private sealed record RuleStep(IReadOnlyList<object> Outputs, IReadOnlyList<CapturedHttpRequest> Requests);

    /// <summary>The rule inputs one step gives; null means "not given". Both surfaces send the same values.</summary>
    private sealed record RuleInputs
    {
        public int? Priority { get; init; }
        public string? Access { get; init; }
        public string? Direction { get; init; }
        public string? Protocol { get; init; }
        public IReadOnlyList<string>? SourceAddressPrefixes { get; init; }
        public IReadOnlyList<string>? SourcePortRanges { get; init; }
        public IReadOnlyList<string>? DestinationAddressPrefixes { get; init; }
        public IReadOnlyList<string>? DestinationPortRanges { get; init; }
        public string? Description { get; init; }

        public List<string> ToCli()
        {
            var args = new List<string>();
            if (Priority is { } priority) args.AddRange(["--priority", priority.ToString(CultureInfo.InvariantCulture)]);
            if (Access is not null) args.AddRange(["--access", Access]);
            if (Direction is not null) args.AddRange(["--direction", Direction]);
            if (Protocol is not null) args.AddRange(["--protocol", Protocol]);
            if (DestinationAddressPrefixes is not null) args.AddRange(["--destination-address-prefixes", .. DestinationAddressPrefixes]);
            if (SourceAddressPrefixes is not null) args.AddRange(["--source-address-prefixes", .. SourceAddressPrefixes]);
            if (SourcePortRanges is not null) args.AddRange(["--source-port-ranges", .. SourcePortRanges]);
            if (DestinationPortRanges is not null) args.AddRange(["--destination-port-ranges", .. DestinationPortRanges]);
            if (Description is not null) args.AddRange(["--description", Description]);
            return args;
        }

        /// <summary>Adds the given inputs with the types the security rule cmdlets bind: <c>int</c> and <c>string[]</c>.</summary>
        public Dictionary<string, object> ToPowerShell(Dictionary<string, object> parameters)
        {
            if (Priority is { } priority) parameters["Priority"] = priority;
            if (Access is not null) parameters["Access"] = Access;
            if (Direction is not null) parameters["Direction"] = Direction;
            if (Protocol is not null) parameters["Protocol"] = Protocol;
            if (DestinationAddressPrefixes is not null) parameters["DestinationAddressPrefixes"] = DestinationAddressPrefixes.ToArray();
            if (SourceAddressPrefixes is not null) parameters["SourceAddressPrefixes"] = SourceAddressPrefixes.ToArray();
            if (SourcePortRanges is not null) parameters["SourcePortRanges"] = SourcePortRanges.ToArray();
            if (DestinationPortRanges is not null) parameters["DestinationPortRanges"] = DestinationPortRanges.ToArray();
            if (Description is not null) parameters["Description"] = Description;
            return parameters;
        }
    }

    /// <summary>
    /// Runs one step on one surface and returns its output with the requests it sent, so each
    /// step's request bodies and order are checked on that step alone.
    /// </summary>
    private sealed class RuleSurface(ScenarioHost host, RecordPlaybackInterceptor interceptor, ClientType clientType, string subscriptionId, string groupName)
    {
        public RuleStep CreateGroup() => Run(
            ["group", "create", "--name", groupName, "--location", Location],
            "New-AzResourceGroup",
            new() { ["Name"] = groupName, ["Location"] = Location },
            extension: "Resources");

        public RuleStep CreateNsg(string nsgName, string? tagKey = null, string? tagValue = null)
        {
            var cli = new List<string> { "network", "nsg", "create", "--name", nsgName, "--resource-group", groupName };
            var ps = new Dictionary<string, object> { ["Name"] = nsgName, ["ResourceGroupName"] = groupName };
            if (tagKey is not null)
            {
                cli.AddRange(["--tags", $"{tagKey}={tagValue}"]);
                // The Network cmdlets turn their -Tag hashtable into this dictionary.
                ps["Tag"] = new Dictionary<string, string> { [tagKey] = tagValue ?? string.Empty };
            }
            return Run([.. cli], "New-AzNetworkSecurityGroup", ps);
        }

        public RuleStep DeleteNsg(string nsgName) => Run(
            ["network", "nsg", "delete", "--name", nsgName, "--resource-group", groupName],
            "Remove-AzNetworkSecurityGroup",
            new() { ["Name"] = nsgName, ["ResourceGroupName"] = groupName });

        public RuleStep CreateRule(string nsgName, string ruleName, RuleInputs inputs) => Run(
            [.. RuleArgs("create", nsgName, ruleName), .. inputs.ToCli()],
            "New-AzNetworkSecurityRule",
            inputs.ToPowerShell(RuleParameters(nsgName, ruleName)));

        public RuleStep UpdateRule(string nsgName, string ruleName, RuleInputs inputs) => Run(
            [.. RuleArgs("update", nsgName, ruleName), .. inputs.ToCli()],
            "Set-AzNetworkSecurityRule",
            inputs.ToPowerShell(RuleParameters(nsgName, ruleName)));

        public RuleStep ShowRule(string nsgName, string ruleName) => Run(
            RuleArgs("show", nsgName, ruleName),
            "Get-AzNetworkSecurityRule_Get",
            RuleParameters(nsgName, ruleName));

        public RuleStep ListRules(string nsgName) => Run(
            ["network", "nsg", "rule", "list", "--nsg-name", nsgName, "--resource-group", groupName],
            "Get-AzNetworkSecurityRule_List",
            new() { ["NetworkSecurityGroupName"] = nsgName, ["ResourceGroupName"] = groupName });

        public RuleStep DeleteRule(string nsgName, string ruleName) => Run(
            RuleArgs("delete", nsgName, ruleName),
            "Remove-AzNetworkSecurityRule",
            RuleParameters(nsgName, ruleName));

        private string[] RuleArgs(string action, string nsgName, string ruleName) =>
            ["network", "nsg", "rule", action, "--name", ruleName, "--nsg-name", nsgName, "--resource-group", groupName];

        private Dictionary<string, object> RuleParameters(string nsgName, string ruleName) => new()
        {
            ["Name"] = ruleName,
            ["NetworkSecurityGroupName"] = nsgName,
            ["ResourceGroupName"] = groupName,
        };

        private RuleStep Run(string[] cliArgs, string psCommand, Dictionary<string, object> psParameters, string extension = "Network")
        {
            int start = interceptor.Sent.Count;
            IReadOnlyList<object> outputs;
            if (clientType == ClientType.CLI)
            {
                outputs = host.RunCli([.. cliArgs, "--subscription-id", subscriptionId]);
            }
            else
            {
                psParameters["SubscriptionId"] = subscriptionId;
                outputs = host.RunPowerShell(psCommand, psParameters, extension);
            }
            return new RuleStep(outputs, [.. interceptor.Sent.Skip(start)]);
        }
    }
}
