// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Network;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises public IP addresses through the real dispatcher/binder and ARM request pipeline on
/// both client surfaces. Record mode runs the CLI steps against live Azure in a run-scoped resource
/// group; playback replays that one recording through the CLI, then through PowerShell.
/// <para>
/// Playback matches request bodies as well as URIs, so every PowerShell step is held to the exact
/// body its CLI step recorded, value casing included. The steps that depend on PowerShell binding
/// run the real cmdlet classes in a runspace: the hashtable <c>-Tag</c>, the
/// <c>-IpAddressVersion</c> alias, and <c>-Force</c>/<c>-PassThru</c> on remove.
/// </para>
/// <para>
/// <see cref="AttachedPublicIpAddress_FormatsItsAddressAndConfiguration"/> needs no recording: it
/// checks how PowerShell formats a public IP attached to an IP configuration.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class PublicIpScenarioTests
{
    // Playback matches the placeholder the sanitisers wrote into the recording.
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string EastUs2 = "eastus2";
    private const string WestUs = "westus";
    private const string EdgeZone = "microsoftlosangeles1";
    private const string PipType = "Microsoft.Network/publicIPAddresses";
    private const string NetworkExtension = "Network";

    // Base names carry no digits: the sanitiser strips the run suffix with (scenario-[a-z-]+)-t<hex>.
    private const string LifecycleGroup = "scenario-pip-life-rg";
    private const string VariantsGroup = "scenario-pip-variants-rg";
    private const string EdgeZoneGroup = "scenario-pip-edge-rg";
    private const string IpA = "scenario-pip-a";
    private const string IpB = "scenario-pip-b";
    private const string IpC = "scenario-pip-c";
    private const string IpD = "scenario-pip-d";
    private const string IpE = "scenario-pip-e";
    private const string IpF = "scenario-pip-f";
    private const string IpG = "scenario-pip-g";
    private const string IpH = "scenario-pip-h";
    private const string IpI = "scenario-pip-i";
    private const string DnsLabel = "scenario-pip-dns";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Network];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttachedPublicIpAddress_FormatsItsAddressAndConfiguration(bool expanded)
    {
        var configuration = expanded
            ? """{ "id": "/nic/ipConfigurations/ipconfig1", "properties": { "privateIPAddress": "10.0.0.4" } }"""
            : """{ "id": "/nic/ipConfigurations/ipconfig1" }""";
        var pip = JsonSerializer.Deserialize<PublicIPAddress>($$"""
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
        Assert.Matches(@"IpConfigurationId\s+: /nic/ipConfigurations/ipconfig1", rendered);
        if (expanded)
        {
            Assert.Matches(@"PrivateIPAddress\s+: 10\.0\.0\.4", rendered);
        }
        Assert.DoesNotMatch(@"(?m)^IpConfiguration\s+:", rendered);
        Assert.DoesNotMatch(@"(?m)^Properties\s+:", rendered);
    }

    // T1: verifies "T1 Public IP lifecycle with defaults, DNS settings and remove confirmation"
    // of network-public-ip-addresses-tests.
    [Fact]
    public void CreateShowListDeletePublicIp() => RunRecordedScenario(nameof(CreateShowListDeletePublicIp), LifecycleGroup, s =>
    {
        string group = s.Scope(LifecycleGroup);
        string ip1 = s.Scope(IpA);
        string ip2 = s.Scope(IpB);
        string dns = s.Scope(DnsLabel);

        // 1
        CreateGroup(s, group, EastUs2);

        // 2: no location, so it is read from the resource group; DNS settings carry only what was given.
        var (outputs, sent) = s.Run(
            CreateArgs(s, group, ip1, "--dns-name", dns, "--dns-name-scope", "TenantReuse", "--allocation-method", "static"),
            ps => ps.RunPowerShell("New-AzPublicIpAddress", PsArgs(s, group, ip1,
                ("DomainNameLabel", dns), ("DomainNameLabelScope", "TenantReuse"), ("AllocationMethod", "static")), NetworkExtension));
        var body = AssertPutAfterGroupRead(sent, s, group, ip1);
        Assert.Equal(EastUs2, body.GetProperty("location").GetString());
        AssertSku(body, "Standard", tier: null);
        var properties = body.GetProperty("properties");
        Assert.Equal("static", properties.GetProperty("publicIPAllocationMethod").GetString());
        Assert.Equal("IPv4", properties.GetProperty("publicIPAddressVersion").GetString());
        var dnsSettings = properties.GetProperty("dnsSettings");
        AssertKeys(dnsSettings, "domainNameLabel", "domainNameLabelScope");
        Assert.Equal(dns, dnsSettings.GetProperty("domainNameLabel").GetString());
        Assert.Equal("TenantReuse", dnsSettings.GetProperty("domainNameLabelScope").GetString());
        var pip = AssertPip(Assert.Single(outputs), ip1);
        Assert.Equal("Succeeded", pip.ProvisioningState);
        Assert.Equal("Standard", pip.SkuName);
        Assert.Equal("Static", pip.PublicIPAllocationMethod);
        Assert.Equal(dns, pip.DnsSettingsDomainNameLabel);
        Assert.Equal("TenantReuse", pip.DnsSettingsDomainNameLabelScope);

        // 3: every default; nothing optional reaches the body.
        (outputs, sent) = s.Run(
            CreateArgs(s, group, ip2),
            ps => ps.RunPowerShell("New-AzPublicIpAddress", PsArgs(s, group, ip2), NetworkExtension));
        body = AssertPutAfterGroupRead(sent, s, group, ip2);
        Assert.Equal(EastUs2, body.GetProperty("location").GetString());
        AssertSku(body, "Standard", tier: null);
        Assert.False(body.TryGetProperty("extendedLocation", out _), "no edge zone was given, so no extendedLocation is sent");
        properties = body.GetProperty("properties");
        Assert.Equal("Static", properties.GetProperty("publicIPAllocationMethod").GetString());
        Assert.Equal("IPv4", properties.GetProperty("publicIPAddressVersion").GetString());
        Assert.False(properties.TryGetProperty("dnsSettings", out _), "no DNS input was given, so no dnsSettings are sent");
        pip = AssertPip(Assert.Single(outputs), ip2);
        Assert.Equal("Succeeded", pip.ProvisioningState);
        Assert.Equal("Standard", pip.SkuName);
        Assert.Equal("Static", pip.PublicIPAllocationMethod);
        Assert.Equal("IPv4", pip.PublicIPAddressVersion);
        Assert.Null(pip.DnsSettingsDomainNameLabel);

        // 4
        (outputs, sent) = s.Run(
            ListArgs(s, group),
            ps => ps.RunPowerShell("Get-AzPublicIpAddress_List", PsArgs(s, group, name: null), NetworkExtension));
        AssertGroupListRequest(sent, s, group);
        Assert.Equal(
            new[] { ip1, ip2 }.Order(StringComparer.Ordinal).ToArray(),
            Listed(outputs).Select(listed => listed.Name).Order(StringComparer.Ordinal).ToArray());

        // 5
        (outputs, _) = s.Run(
            ShowArgs(s, group, ip1),
            ps => ps.RunPowerShell("Get-AzPublicIpAddress_Get", PsArgs(s, group, ip1), NetworkExtension));
        pip = AssertPip(Assert.Single(outputs), ip1);
        Assert.Equal(EastUs2, pip.Location);
        Assert.Equal("Static", pip.PublicIPAllocationMethod);
        Assert.Equal(dns, pip.DnsSettingsDomainNameLabel);

        // 6: the real cmdlet, so -Force and -PassThru are handled by it. A runspace with no UI cannot
        // answer a prompt, so completing without one shows -Force skipped the confirmation.
        (outputs, sent) = s.Run(
            DeleteArgs(s, group, ip1),
            ps => ps.RunPowerShellCmdlet<RemovePublicIpAddressCmdlet>(
                new Dictionary<string, object>(PsArgs(s, group, ip1)) { ["Force"] = true, ["PassThru"] = true }));
        Assert.Contains(sent, request => request.Method == "DELETE" && PathEndsWith(request, $"/publicIPAddresses/{ip1}"));
        if (s.IsCli)
        {
            Assert.Empty(outputs);
        }
        else
        {
            Assert.True(Assert.IsType<bool>(Assert.Single(outputs)));
        }

        // 7
        (outputs, _) = s.Run(
            ListArgs(s, group),
            ps => ps.RunPowerShell("Get-AzPublicIpAddress_List", PsArgs(s, group, name: null), NetworkExtension));
        AssertPip(Assert.Single(Listed(outputs)), ip2);
    });

    // T2: verifies "T2 Create variants: SKU, tier, allocation, version, zones, tags, DDoS mode, idle
    // timeout, retired Basic SKU" of network-public-ip-addresses-tests.
    [Fact]
    public void CreatesPublicIpVariants() => RunRecordedScenario(nameof(CreatesPublicIpVariants), VariantsGroup, s =>
    {
        string group = s.Scope(VariantsGroup);

        // 1
        CreateGroup(s, group, EastUs2);

        // 2: an explicit location, so no resource-group read.
        string ip1 = s.Scope(IpA);
        var (outputs, sent) = s.Run(
            CreateArgs(s, group, ip1, "--location", EastUs2),
            ps => ps.RunPowerShell("New-AzPublicIpAddress", PsArgs(s, group, ip1, ("Location", EastUs2)), NetworkExtension));
        Assert.DoesNotContain(sent, request => IsGroupRead(request, s.SubscriptionId, group));
        var body = PutBody(sent, s, group, ip1);
        Assert.Equal(EastUs2, body.GetProperty("location").GetString());
        AssertSku(body, "Standard", tier: null);
        Assert.Equal("Static", Properties(body).GetProperty("publicIPAllocationMethod").GetString());
        var pip = AssertPip(Assert.Single(outputs), ip1);
        Assert.Equal("Standard", pip.SkuName);
        Assert.Equal("Regional", pip.SkuTier);
        Assert.Equal("Static", pip.PublicIPAllocationMethod);

        // 3: the real cmdlet, so the hashtable tag is converted before it reaches the body.
        string ip2 = s.Scope(IpB);
        (outputs, sent) = s.Run(
            CreateArgs(s, group, ip2, "--location", EastUs2, "--sku", "Standard", "--tags", "foo=doo"),
            ps => ps.RunPowerShellCmdlet<NewPublicIpAddressCmdlet>(PsArgs(s, group, ip2,
                ("Location", EastUs2), ("Sku", "Standard"), ("Tag", new Hashtable { ["foo"] = "doo" }))));
        body = PutBody(sent, s, group, ip2);
        AssertSku(body, "Standard");
        Assert.Equal("Static", Properties(body).GetProperty("publicIPAllocationMethod").GetString());
        var tag = Assert.Single(body.GetProperty("tags").EnumerateObject());
        Assert.Equal("foo", tag.Name);
        Assert.Equal("doo", tag.Value.GetString());
        pip = AssertPip(Assert.Single(outputs), ip2);
        Assert.Equal("Standard", pip.SkuName);
        Assert.Equal("Regional", pip.SkuTier);
        Assert.Equal("Static", pip.PublicIPAllocationMethod);
        Assert.NotNull(pip.Tags);
        var outputTag = Assert.Single(pip.Tags);
        Assert.Equal("foo", outputTag.Key);
        Assert.Equal("doo", outputTag.Value);

        // 4
        string ip3 = s.Scope(IpC);
        (outputs, sent) = s.Run(
            CreateArgs(s, group, ip3, "--location", EastUs2, "--sku", "Standard", "--tier", "Global"),
            ps => ps.RunPowerShell("New-AzPublicIpAddress", PsArgs(s, group, ip3,
                ("Location", EastUs2), ("Sku", "Standard"), ("Tier", "Global")), NetworkExtension));
        body = PutBody(sent, s, group, ip3);
        AssertSku(body, "Standard", tier: "Global");
        Assert.Equal("Static", Properties(body).GetProperty("publicIPAllocationMethod").GetString());
        pip = AssertPip(Assert.Single(outputs), ip3);
        Assert.Equal("Standard", pip.SkuName);
        Assert.Equal("Global", pip.SkuTier);
        Assert.Equal("Static", pip.PublicIPAllocationMethod);

        // 5: no location, so it is read from the resource group; the given casing is sent as is.
        string ip4 = s.Scope(IpD);
        (outputs, sent) = s.Run(
            CreateArgs(s, group, ip4, "--sku", "standardv2", "--allocation-method", "static"),
            ps => ps.RunPowerShell("New-AzPublicIpAddress", PsArgs(s, group, ip4,
                ("Sku", "standardv2"), ("AllocationMethod", "static")), NetworkExtension));
        body = AssertPutAfterGroupRead(sent, s, group, ip4);
        Assert.Equal(EastUs2, body.GetProperty("location").GetString());
        AssertSku(body, "standardv2");
        Assert.Equal("static", Properties(body).GetProperty("publicIPAllocationMethod").GetString());
        pip = AssertPip(Assert.Single(outputs), ip4);
        Assert.Equal("Succeeded", pip.ProvisioningState);
        Assert.Equal("StandardV2", pip.SkuName);
        Assert.NotEqual(true, pip.UpgradedToV2);

        // 6
        string ip5 = s.Scope(IpE);
        (outputs, sent) = s.Run(
            CreateArgs(s, group, ip5, "--location", EastUs2, "--version", "ipv4", "--sku", "standard", "-z", "1", "2", "3"),
            ps => ps.RunPowerShell("New-AzPublicIpAddress", PsArgs(s, group, ip5,
                ("Location", EastUs2), ("Version", "ipv4"), ("Sku", "standard"), ("Zone", new[] { "1", "2", "3" })), NetworkExtension));
        body = PutBody(sent, s, group, ip5);
        AssertSku(body, "standard");
        var properties = Properties(body);
        Assert.Equal("ipv4", properties.GetProperty("publicIPAddressVersion").GetString());
        Assert.Equal("Static", properties.GetProperty("publicIPAllocationMethod").GetString());
        Assert.False(properties.TryGetProperty("dnsSettings", out _), "no DNS input was given, so no dnsSettings are sent");
        Assert.Equal(new[] { "1", "2", "3" }, body.GetProperty("zones").EnumerateArray().Select(zone => zone.GetString()).ToArray());
        pip = AssertPip(Assert.Single(outputs), ip5);
        Assert.Equal("Static", pip.PublicIPAllocationMethod);
        Assert.Equal("IPv4", pip.PublicIPAddressVersion);
        Assert.NotNull(pip.Zones);
        Assert.Equal(new[] { "1", "2", "3" }, pip.Zones.ToArray());
        Assert.Null(pip.DnsSettingsDomainNameLabel);

        // 7: the real cmdlet, so the -IpAddressVersion alias binds to -Version.
        string ip6 = s.Scope(IpF);
        (outputs, sent) = s.Run(
            CreateArgs(s, group, ip6, "--location", EastUs2, "--allocation-method", "Static", "--version", "IPv6"),
            ps => ps.RunPowerShellCmdlet<NewPublicIpAddressCmdlet>(PsArgs(s, group, ip6,
                ("Location", EastUs2), ("AllocationMethod", "Static"), ("IpAddressVersion", "IPv6"))));
        body = PutBody(sent, s, group, ip6);
        AssertSku(body, "Standard");
        properties = Properties(body);
        Assert.Equal("IPv6", properties.GetProperty("publicIPAddressVersion").GetString());
        Assert.Equal("Static", properties.GetProperty("publicIPAllocationMethod").GetString());
        pip = AssertPip(Assert.Single(outputs), ip6);
        Assert.Equal("Succeeded", pip.ProvisioningState);
        Assert.Equal("IPv6", pip.PublicIPAddressVersion);
        Assert.Equal("Static", pip.PublicIPAllocationMethod);

        // 8: DDoS IP protection is billed while the address exists.
        string ip7 = s.Scope(IpG);
        (outputs, sent) = s.Run(
            CreateArgs(s, group, ip7, "--location", EastUs2, "--sku", "Standard", "--protection-mode", "Enabled"),
            ps => ps.RunPowerShell("New-AzPublicIpAddress", PsArgs(s, group, ip7,
                ("Location", EastUs2), ("Sku", "Standard"), ("DdosProtectionMode", "Enabled")), NetworkExtension));
        var ddosSettings = Properties(PutBody(sent, s, group, ip7)).GetProperty("ddosSettings");
        Assert.Equal("Enabled", ddosSettings.GetProperty("protectionMode").GetString());
        Assert.False(ddosSettings.TryGetProperty("ddosProtectionPlan", out _), "no DDoS protection plan was given");
        pip = AssertPip(Assert.Single(outputs), ip7);
        Assert.Equal("Enabled", pip.DdosSettingsProtectionMode);
        Assert.Equal("Static", pip.PublicIPAllocationMethod);

        // 9
        string ip8 = s.Scope(IpH);
        (outputs, sent) = s.Run(
            CreateArgs(s, group, ip8, "--location", EastUs2, "--allocation-method", "Static", "--idle-timeout", "15"),
            ps => ps.RunPowerShell("New-AzPublicIpAddress", PsArgs(s, group, ip8,
                ("Location", EastUs2), ("AllocationMethod", "Static"), ("IdleTimeoutInMinutes", 15)), NetworkExtension));
        Assert.Equal(15, Properties(PutBody(sent, s, group, ip8)).GetProperty("idleTimeoutInMinutes").GetInt32());
        pip = AssertPip(Assert.Single(outputs), ip8);
        Assert.Equal(15, pip.IdleTimeoutInMinutes);
        Assert.Equal("Static", pip.PublicIPAllocationMethod);

        // 10: Basic SKU public IPs are retired, so the service rejects the create.
        string ip9 = s.Scope(IpI);
        var (error, failedOutputs, failedSent) = s.TryRun(
            CreateArgs(s, group, ip9, "--location", EastUs2, "--sku", "Basic", "--allocation-method", "Dynamic"),
            ps => ps.RunPowerShell("New-AzPublicIpAddress", PsArgs(s, group, ip9,
                ("Location", EastUs2), ("Sku", "Basic"), ("AllocationMethod", "Dynamic")), NetworkExtension));
        body = PutBody(failedSent, s, group, ip9);
        AssertSku(body, "Basic");
        Assert.Equal("Dynamic", Properties(body).GetProperty("publicIPAllocationMethod").GetString());
        Assert.Null(failedOutputs);
        Assert.NotNull(error);
        // Task 3.3.4 pins the recorded error code here once the scenario is recorded.
        AssertServiceRejected(error);
    });

    // T3: verifies "T3 Create in an edge zone" of network-public-ip-addresses-tests.
    [Fact]
    public void CreatesPublicIpInEdgeZone()
    {
        // Set by the CLI replay and checked by the PowerShell one, so both surfaces take the same branch.
        bool? cliCreated = null;

        RunRecordedScenario(nameof(CreatesPublicIpInEdgeZone), EdgeZoneGroup, s =>
        {
            string group = s.Scope(EdgeZoneGroup);
            string ip1 = s.Scope(IpA);

            // 1
            CreateGroup(s, group, WestUs);

            // 2
            var (error, outputs, sent) = s.TryRun(
                CreateArgs(s, group, ip1, "--location", WestUs, "--edge-zone", EdgeZone, "--sku", "Standard"),
                ps => ps.RunPowerShell("New-AzPublicIpAddress", PsArgs(s, group, ip1,
                    ("Location", WestUs), ("EdgeZone", EdgeZone), ("Sku", "Standard")), NetworkExtension));
            var body = PutBody(sent, s, group, ip1);
            var extendedLocation = body.GetProperty("extendedLocation");
            AssertKeys(extendedLocation, "name", "type");
            Assert.Equal(EdgeZone, extendedLocation.GetProperty("name").GetString());
            Assert.Equal("EdgeZone", extendedLocation.GetProperty("type").GetString());
            Assert.Equal(WestUs, body.GetProperty("location").GetString());
            AssertSku(body, "Standard");
            Assert.Equal("Static", Properties(body).GetProperty("publicIPAllocationMethod").GetString());

            // Either outcome is accepted until task 3.3.4 keeps the recorded one: the address in the
            // edge zone, or the service error when the subscription has no access to that edge zone.
            bool created = error is null;
            if (created)
            {
                var pip = AssertPip(Assert.Single(outputs!), ip1);
                Assert.Equal(EdgeZone, pip.ExtendedLocationName);
                Assert.Equal("EdgeZone", pip.ExtendedLocationType);
            }
            else
            {
                Assert.Null(outputs);
                AssertServiceRejected(error!);
            }

            if (s.IsCli)
            {
                cliCreated = created;
            }
            else
            {
                Assert.Equal(cliCreated, created);
            }
        });
    }

    /// <summary>
    /// One surface of one run: the host, the interceptor whose <c>Sent</c> list slices out each
    /// step's requests, the subscription, and the name scoping for this mode.
    /// </summary>
    private sealed record Surface(
        ClientType Client, ScenarioHost Host, RecordPlaybackInterceptor Requests, string SubscriptionId, Func<string, string> Scope)
    {
        public bool IsCli => Client == ClientType.CLI;

        /// <summary>Runs the step on this surface and returns its output with the requests it sent, in send order.</summary>
        public (IReadOnlyList<object> Outputs, IReadOnlyList<CapturedHttpRequest> Sent) Run(
            string[] cliArgs, Func<ScenarioHost, IReadOnlyList<object>> powerShell)
        {
            int start = Requests.Sent.Count;
            var outputs = IsCli ? Host.RunCli(cliArgs) : powerShell(Host);
            IReadOnlyList<CapturedHttpRequest> sent = [.. Requests.Sent.Skip(start)];
            return (outputs, sent);
        }

        /// <summary>Like <see cref="Run"/>, for a step that may fail: the error, or the output when there is none.</summary>
        public (Exception? Error, IReadOnlyList<object>? Outputs, IReadOnlyList<CapturedHttpRequest> Sent) TryRun(
            string[] cliArgs, Func<ScenarioHost, IReadOnlyList<object>> powerShell)
        {
            int start = Requests.Sent.Count;
            IReadOnlyList<object>? outputs = null;
            var error = Record.Exception(() => { outputs = IsCli ? Host.RunCli(cliArgs) : powerShell(Host); });
            IReadOnlyList<CapturedHttpRequest> sent = [.. Requests.Sent.Skip(start)];
            return (error, outputs, sent);
        }
    }

    /// <summary>
    /// Record mode runs the CLI steps once against live Azure with run-scoped names, saves the
    /// recording, then deletes the resource group. Playback replays the recording through the CLI,
    /// then through PowerShell, with the sanitised base names.
    /// </summary>
    private static void RunRecordedScenario(string testName, string groupBaseName, Action<Surface> steps)
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(PublicIpScenarioTests), testName, new DefaultRequestMatcher(matchBody: true));

        if (recording.Mode == RecorderMode.Record)
        {
            string Scoped(string baseName) => TestRun.ScopedName(baseName, recording.Mode);
            var interceptor = recording.CreateInterceptor();
            using var host = new ScenarioHost(ClientType.CLI, interceptor, useLiveAuth: true, extensions: s_extensions);
            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                steps(new Surface(ClientType.CLI, host, interceptor, subscriptionId, Scoped));
                recording.Save();
            }
            finally
            {
                // After the save, so the delete never lands in the recording.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", Scoped(groupBaseName)); } catch { }
            }
            return;
        }

        foreach (var client in new[] { ClientType.CLI, ClientType.PowerShell })
        {
            var interceptor = recording.CreateInterceptor();
            using var host = new ScenarioHost(client, interceptor, extensions: s_extensions);
            steps(new Surface(client, host, interceptor, SubscriptionId, baseName => baseName));
        }
    }

    private static void CreateGroup(Surface s, string group, string location) => s.Run(
        ["group", "create", "--subscription-id", s.SubscriptionId, "--name", group, "--location", location],
        ps => ps.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
        {
            ["SubscriptionId"] = s.SubscriptionId,
            ["Name"] = group,
            ["Location"] = location,
        }, "Resources"));

    private static string[] CreateArgs(Surface s, string group, string name, params string[] options) =>
        [.. CommandArgs("create", s, group), "--name", name, .. options];

    private static string[] ShowArgs(Surface s, string group, string name) =>
        [.. CommandArgs("show", s, group), "--name", name];

    private static string[] DeleteArgs(Surface s, string group, string name) =>
        [.. CommandArgs("delete", s, group), "--name", name];

    private static string[] ListArgs(Surface s, string group) => CommandArgs("list", s, group);

    private static string[] CommandArgs(string action, Surface s, string group) =>
        ["network", "public-ip", action, "--subscription-id", s.SubscriptionId, "--resource-group", group];

    private static Dictionary<string, object> PsArgs(Surface s, string group, string? name, params (string Key, object Value)[] options)
    {
        var parameters = new Dictionary<string, object>
        {
            ["SubscriptionId"] = s.SubscriptionId,
            ["ResourceGroupName"] = group,
        };
        if (name is not null) parameters["Name"] = name;
        foreach (var (key, value) in options) parameters[key] = value;
        return parameters;
    }

    /// <summary>The CLI writes a list as one collection; the PowerShell surface may enumerate it.</summary>
    private static List<PublicIPAddress> Listed(IReadOnlyList<object> outputs) =>
        [.. outputs.SelectMany(output => output is PublicIPAddress single
            ? new[] { single }
            : Assert.IsAssignableFrom<IEnumerable<PublicIPAddress>>(output))];

    private static PublicIPAddress AssertPip(object output, string name)
    {
        var pip = Assert.IsType<PublicIPAddress>(output);
        Assert.Equal(name, pip.Name);
        Assert.Equal(PipType, pip.Type);
        return pip;
    }

    /// <summary>The step's single PUT of <paramref name="name"/>, sent after a GET of the resource group.</summary>
    private static JsonElement AssertPutAfterGroupRead(IReadOnlyList<CapturedHttpRequest> sent, Surface s, string group, string name)
    {
        int read = IndexOf(sent, request => IsGroupRead(request, s.SubscriptionId, group));
        int put = IndexOf(sent, request => IsPipPut(request, name));
        Assert.True(read >= 0, $"no GET of resource group '{group}' was sent");
        Assert.True(put >= 0, $"no PUT of '{name}' was sent");
        Assert.True(read < put, "the resource-group GET must be sent before the public IP PUT");
        return PutBody(sent, s, group, name);
    }

    /// <summary>The body of the step's single PUT of <paramref name="name"/>.</summary>
    private static JsonElement PutBody(IReadOnlyList<CapturedHttpRequest> sent, Surface s, string group, string name)
    {
        var put = Assert.Single(sent, request => IsPipPut(request, name));
        Assert.True(
            PathEndsWith(put, $"/subscriptions/{s.SubscriptionId}/resourceGroups/{group}/providers/Microsoft.Network/publicIPAddresses/{name}"),
            $"unexpected PUT URI {put.Uri}");
        Assert.NotNull(put.Body);
        using var document = JsonDocument.Parse(put.Body);
        return document.RootElement.Clone();
    }

    private static JsonElement Properties(JsonElement body) => body.GetProperty("properties");

    private static void AssertSku(JsonElement body, string name) =>
        Assert.Equal(name, body.GetProperty("sku").GetProperty("name").GetString());

    private static void AssertSku(JsonElement body, string name, string? tier)
    {
        AssertSku(body, name);
        var sku = body.GetProperty("sku");
        if (tier is null)
        {
            Assert.False(sku.TryGetProperty("tier", out _), "no tier was given, so no sku.tier is sent");
        }
        else
        {
            Assert.Equal(tier, sku.GetProperty("tier").GetString());
        }
    }

    private static void AssertKeys(JsonElement element, params string[] expected) =>
        Assert.Equal(
            expected.Order(StringComparer.Ordinal).ToArray(),
            element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());

    private static void AssertGroupListRequest(IReadOnlyList<CapturedHttpRequest> sent, Surface s, string group)
    {
        Assert.NotEmpty(sent);
        Assert.All(sent, request => Assert.Equal("GET", request.Method));
        Assert.True(
            PathEndsWith(sent[0], $"/subscriptions/{s.SubscriptionId}/resourceGroups/{group}/providers/Microsoft.Network/publicIPAddresses"),
            $"unexpected list URI {sent[0].Uri}");
    }

    /// <summary>A 4xx service error; the exact code is pinned once the scenario is recorded.</summary>
    private static void AssertServiceRejected(Exception error)
    {
        if (error is HttpRequestException { StatusCode: { } status })
        {
            Assert.InRange((int)status, 400, 499);
        }
        else
        {
            Assert.Matches(@"\b4\d\d\b", error.ToString());
        }
    }

    private static bool PathEndsWith(CapturedHttpRequest request, string suffix) =>
        new Uri(request.Uri).AbsolutePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

    private static bool IsGroupRead(CapturedHttpRequest request, string subscriptionId, string group) =>
        request.Method == "GET" && PathEndsWith(request, $"/subscriptions/{subscriptionId}/resourcegroups/{group}");

    private static bool IsPipPut(CapturedHttpRequest request, string name) =>
        request.Method == "PUT" && PathEndsWith(request, $"/publicIPAddresses/{name}");

    private static int IndexOf(IReadOnlyList<CapturedHttpRequest> sent, Func<CapturedHttpRequest, bool> match)
    {
        for (int i = 0; i < sent.Count; i++)
        {
            if (match(sent[i])) return i;
        }
        return -1;
    }
}
