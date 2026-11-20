using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Network;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises the network interface commands against live Azure through the real dispatcher,
/// binder and ARM request pipeline. The lifecycle is recorded from the CLI and replayed on both
/// surfaces from the one recording, matching request bodies, so the PowerShell surface must send
/// exactly what the CLI sent. The real-cmdlet test runs the PowerShell cmdlet classes in a
/// runspace with a recording of its own.
/// </summary>
[Collection("Scenario Tests")]
public class NetworkInterfaceScenarioTests
{
    private const string PlaybackSubscriptionId = RecordingSanitizers.PlaceholderGuid;
    private const string Location = "eastus2";

    private const string GroupBase = "scenario-nic-rg";
    private const string VNetBase = "scenario-nic-vnet";
    private const string SubnetBase = "scenario-nic-subnet";
    private const string PublicIpBase = "scenario-nic-pip";
    private const string NsgBase = "scenario-nic-nsg";
    private const string SecondNsgBase = "scenario-nic-nsg-second";
    private const string NicBase = "scenario-nic";
    private const string SecondNicBase = "scenario-nic-second";

    private const string CmdletGroupBase = "scenario-nic-cmdlet-rg";
    private const string CmdletVNetBase = "scenario-nic-cmdlet-vnet";
    private const string CmdletSubnetBase = "scenario-nic-cmdlet-subnet";
    private const string CmdletPublicIpBase = "scenario-nic-cmdlet-pip";
    private const string CmdletNsgBase = "scenario-nic-cmdlet-nsg";
    private const string CmdletNicBase = "scenario-nic-cmdlet";

    /// <summary>
    /// Case T1, verifying test spec scenario "T1 Network interface lifecycle on both surfaces":
    /// create with the resource-group location default, by subnet ID with every optional input,
    /// a full replace that attaches a security group by name, then by ID, a second interface with
    /// an explicit location, show with and without $expand, both list scopes, delete, and the
    /// 404 and empty list that follow.
    /// </summary>
    [Fact]
    public void NetworkInterfaceLifecycle_OnBothSurfaces()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(NetworkInterfaceScenarioTests),
            nameof(NetworkInterfaceLifecycle_OnBothSurfaces),
            new DefaultRequestMatcher(matchBody: true),
            // The subscription-scope list returns every interface in the recording subscription;
            // only the two this scenario creates may reach disk.
            RecordingSanitizers.Default.With([new NetworkInterfaceListSanitizer(GroupBase, [NicBase, SecondNicBase])]));

        if (recording.Mode == RecorderMode.Record)
        {
            var names = LifecycleNames.For(recording.Mode);
            var interceptor = recording.CreateInterceptor();
            using var host = new ScenarioHost(
                ClientType.CLI,
                interceptor,
                useLiveAuth: true,
                extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]);

            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                RunLifecycle(step => host.RunCli(step.Cli), interceptor, subscriptionId, names);
                recording.Save();
            }
            finally
            {
                // After the save, so the delete never lands in the recording.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", names.Group); } catch { }
            }
            return;
        }

        var playbackNames = LifecycleNames.For(recording.Mode);

        var cliInterceptor = recording.CreateInterceptor();
        using (var cli = new ScenarioHost(
            ClientType.CLI,
            cliInterceptor,
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]))
        {
            RunLifecycle(step => cli.RunCli(step.Cli), cliInterceptor, PlaybackSubscriptionId, playbackNames);
        }

        var psInterceptor = recording.CreateInterceptor();
        using (var ps = new ScenarioHost(
            ClientType.PowerShell,
            psInterceptor,
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]))
        {
            RunLifecycle(step => ps.RunPowerShell(step.PsCommand, step.Ps, extension: step.PsExtension), psInterceptor, PlaybackSubscriptionId, playbackNames);
        }
    }

    /// <summary>
    /// Case T3, verifying test spec scenario "T3 PowerShell cmdlets bind aliases, route parameter
    /// sets and honor -Force -PassThru": runs the real cmdlet classes in a runspace, the layer
    /// <c>ScenarioHost.RunPowerShell</c> bypasses. The ID aliases, the DnsServer alias, the tag
    /// hashtable and the accelerated-networking switch must bind, Get must route by parameter set
    /// and enumerate the list, and Remove's -Force must skip the High-impact prompt.
    /// </summary>
    [Fact]
    public void PowerShellCmdlets_BindAliases_RouteParameterSets_AndHonorForcePassThru()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(NetworkInterfaceScenarioTests),
            nameof(PowerShellCmdlets_BindAliases_RouteParameterSets_AndHonorForcePassThru),
            new DefaultRequestMatcher(matchBody: true));
        var isRecording = recording.Mode == RecorderMode.Record;
        var groupName = TestRun.ScopedName(CmdletGroupBase, recording.Mode);
        var vnetName = TestRun.ScopedName(CmdletVNetBase, recording.Mode);
        var subnetName = TestRun.ScopedName(CmdletSubnetBase, recording.Mode);
        var publicIpName = TestRun.ScopedName(CmdletPublicIpBase, recording.Mode);
        var nsgName = TestRun.ScopedName(CmdletNsgBase, recording.Mode);
        var nicName = TestRun.ScopedName(CmdletNicBase, recording.Mode);

        using var host = new ScenarioHost(
            ClientType.PowerShell,
            recording.CreateInterceptor(),
            useLiveAuth: isRecording,
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]);
        var subscriptionId = isRecording ? host.LiveSubscriptionId() : PlaybackSubscriptionId;

        var sessionState = InitialSessionState.CreateDefault2();
        sessionState.Commands.Add(new SessionStateCmdletEntry("New-AzNetworkInterface", typeof(NewNetworkInterfaceCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Get-AzNetworkInterface", typeof(GetNetworkInterfaceCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Remove-AzNetworkInterface", typeof(RemoveNetworkInterfaceCmdlet), null));
        using var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();

        var identity = new Dictionary<string, object>
        {
            ["SubscriptionId"] = subscriptionId,
            ["ResourceGroupName"] = groupName,
            ["ResourceName"] = nicName,
        };
        var list = new Dictionary<string, object>
        {
            ["SubscriptionId"] = subscriptionId,
            ["ResourceGroupName"] = groupName,
        };

        try
        {
            // Steps 1 to 4: setup through the invocation path.
            host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = groupName,
                ["Location"] = Location,
            }, extension: "Resources");
            var vnet = Assert.IsType<VirtualNetwork>(Assert.Single(host.RunPowerShell("New-AzVirtualNetwork", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = vnetName,
                ["ResourceGroupName"] = groupName,
                ["Location"] = Location,
                ["AddressPrefixes"] = new List<string> { "10.0.0.0/16" },
                ["SubnetName"] = subnetName,
                ["SubnetPrefixes"] = new List<string> { "10.0.1.0/24" },
            }, extension: "Network")));
            var subnetId = RequireId(Assert.Single(vnet.Subnets ?? []).Id);
            var publicIpId = RequireId(Assert.IsType<PublicIPAddress>(Assert.Single(host.RunPowerShell("New-AzPublicIpAddress", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = publicIpName,
                ["ResourceGroupName"] = groupName,
                ["Location"] = Location,
            }, extension: "Network"))).Id);
            var nsgId = RequireId(Assert.IsType<NetworkSecurityGroup>(Assert.Single(host.RunPowerShell("New-AzNetworkSecurityGroup", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = nsgName,
                ["ResourceGroupName"] = groupName,
                ["Location"] = Location,
            }, extension: "Network"))).Id);

            // Step 5: the ID aliases, DnsServer, the switch and the tag hashtable bind.
            var created = Assert.IsType<NetworkInterface>(Assert.Single(Invoke(runspace, "New-AzNetworkInterface", new(identity)
            {
                ["Location"] = Location,
                ["SubnetId"] = subnetId,
                ["PublicIpAddressId"] = publicIpId,
                ["NetworkSecurityGroupId"] = nsgId,
                ["DnsServer"] = new[] { "100.1.2.3" },
                ["EnableAcceleratedNetworking"] = true,
                ["Tag"] = new Hashtable { ["fastpathenabled"] = "true" },
            })));
            Assert.Equal(nicName, created.Name);
            var ipConfiguration = Assert.Single(created.IpConfigurations ?? []);
            Assert.Equal(subnetId, ipConfiguration.SubnetId, ignoreCase: true);
            Assert.Equal(publicIpId, ipConfiguration.PublicIPAddress?.Id, ignoreCase: true);
            Assert.Equal(nsgId, created.NetworkSecurityGroupId, ignoreCase: true);
            Assert.Equal("100.1.2.3", Assert.Single(created.DnsSettingsDnsServers ?? []));
            Assert.True(created.EnableAcceleratedNetworking);
            AssertTag(created, "fastpathenabled", "true");

            // Step 6: List is the default parameter set and enumerates the list.
            Assert.Equal(nicName, Assert.IsType<NetworkInterface>(Assert.Single(Invoke(runspace, "Get-AzNetworkInterface", list))).Name);

            // Step 7: -ResourceName selects the Get parameter set.
            Assert.Equal(nicName, Assert.IsType<NetworkInterface>(Assert.Single(Invoke(runspace, "Get-AzNetworkInterface", identity))).Name);

            // Step 8: -Force skips the prompt; the delete writes nothing, so -PassThru's true is the only output.
            var removed = Invoke(runspace, "Remove-AzNetworkInterface", new(identity) { ["Force"] = true, ["PassThru"] = true });
            Assert.True(Assert.IsType<bool>(Assert.Single(removed)));

            // Step 9: an empty list enumerates to nothing.
            Assert.Empty(Invoke(runspace, "Get-AzNetworkInterface", list));
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

    /// <summary>The T1 steps, run on one surface. Every step carries both its CLI and PowerShell form.</summary>
    private static void RunLifecycle(Func<Step, IReadOnlyList<object>> run, RecordPlaybackInterceptor interceptor, string subscriptionId, LifecycleNames n)
    {
        // Steps 1 to 5: setup.
        run(new Step(
            ["group", "create", "--subscription-id", subscriptionId, "--name", n.Group, "--location", Location],
            "New-AzResourceGroup",
            new() { ["SubscriptionId"] = subscriptionId, ["Name"] = n.Group, ["Location"] = Location },
            PsExtension: "Resources"));

        var vnet = Assert.IsType<VirtualNetwork>(Assert.Single(run(new Step(
            ["network", "vnet", "create", "--subscription-id", subscriptionId, "--name", n.VNet, "--resource-group", n.Group, "--location", Location,
             "--address-prefixes", "10.0.0.0/16", "--subnet-name", n.Subnet, "--subnet-prefixes", "10.0.0.0/24"],
            "New-AzVirtualNetwork",
            new()
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = n.VNet,
                ["ResourceGroupName"] = n.Group,
                ["Location"] = Location,
                ["AddressPrefixes"] = new List<string> { "10.0.0.0/16" },
                ["SubnetName"] = n.Subnet,
                ["SubnetPrefixes"] = new List<string> { "10.0.0.0/24" },
            }))));
        var subnetId = RequireId(Assert.Single(vnet.Subnets ?? []).Id);

        var publicIpId = RequireId(Assert.IsType<PublicIPAddress>(Assert.Single(run(new Step(
            ["network", "public-ip", "create", "--subscription-id", subscriptionId, "--name", n.PublicIp, "--resource-group", n.Group, "--location", Location],
            "New-AzPublicIpAddress",
            new() { ["SubscriptionId"] = subscriptionId, ["Name"] = n.PublicIp, ["ResourceGroupName"] = n.Group, ["Location"] = Location })))).Id);

        var nsgId = RequireId(Assert.IsType<NetworkSecurityGroup>(Assert.Single(run(new Step(
            ["network", "nsg", "create", "--subscription-id", subscriptionId, "--name", n.Nsg, "--resource-group", n.Group, "--location", Location],
            "New-AzNetworkSecurityGroup",
            new() { ["SubscriptionId"] = subscriptionId, ["Name"] = n.Nsg, ["ResourceGroupName"] = n.Group, ["Location"] = Location })))).Id);

        var secondNsgId = RequireId(Assert.IsType<NetworkSecurityGroup>(Assert.Single(run(new Step(
            ["network", "nsg", "create", "--subscription-id", subscriptionId, "--name", n.Nsg2, "--resource-group", n.Group, "--location", Location],
            "New-AzNetworkSecurityGroup",
            new() { ["SubscriptionId"] = subscriptionId, ["Name"] = n.Nsg2, ["ResourceGroupName"] = n.Group, ["Location"] = Location })))).Id);

        // Step 6: no location, so the create reads the resource group's location first.
        var (outputs, sent) = Capture(interceptor, () => run(new Step(
            ["network", "nic", "create", "--subscription-id", subscriptionId, "--name", n.Nic, "--resource-group", n.Group, "--subnet", n.Subnet, "--vnet-name", n.VNet],
            "New-AzNetworkInterface",
            new()
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = n.Nic,
                ["ResourceGroupName"] = n.Group,
                ["Subnet"] = n.Subnet,
                ["VirtualNetworkName"] = n.VNet,
            })));
        AssertGroupLookupBeforePut(sent, n.Group, n.Nic, expected: true);
        var nic = Assert.IsType<NetworkInterface>(Assert.Single(outputs));
        Assert.Equal(Location, nic.Location);
        Assert.Equal("Succeeded", nic.ProvisioningState);
        var ipConfiguration = Assert.Single(nic.IpConfigurations ?? []);
        Assert.Equal("ipconfig1", ipConfiguration.Name);
        Assert.True(ipConfiguration.Primary);
        Assert.Equal("Dynamic", ipConfiguration.PrivateIPAllocationMethod);
        Assert.Equal("IPv4", ipConfiguration.PrivateIPAddressVersion);
        Assert.Equal(subnetId, ipConfiguration.SubnetId, ignoreCase: true);
        Assert.Null(ipConfiguration.PublicIPAddress);
        Assert.Null(nic.NetworkSecurityGroupId);

        // Step 7: subnet by ID, with every optional input.
        nic = Assert.IsType<NetworkInterface>(Assert.Single(run(new Step(
            ["network", "nic", "create", "--subscription-id", subscriptionId, "--name", n.Nic, "--resource-group", n.Group, "--subnet", subnetId,
             "--ip-forwarding", "true", "--private-ip-address", "10.0.0.15", "--public-ip-address", n.PublicIp, "--internal-dns-name", "test",
             "--dns-servers", "100.1.2.3", "--accelerated-networking", "true", "--tags", "foo=doo"],
            "New-AzNetworkInterface",
            new()
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = n.Nic,
                ["ResourceGroupName"] = n.Group,
                ["Subnet"] = subnetId,
                ["EnableIPForwarding"] = true,
                ["PrivateIpAddress"] = "10.0.0.15",
                ["PublicIpAddress"] = n.PublicIp,
                ["InternalDnsNameLabel"] = "test",
                ["DnsServers"] = new List<string> { "100.1.2.3" },
                ["EnableAcceleratedNetworking"] = true,
                ["Tag"] = new Dictionary<string, string> { ["foo"] = "doo" },
            }))));
        ipConfiguration = Assert.Single(nic.IpConfigurations ?? []);
        Assert.Equal("Static", ipConfiguration.PrivateIPAllocationMethod);
        Assert.Equal("10.0.0.15", ipConfiguration.PrivateIPAddress);
        Assert.Equal(subnetId, ipConfiguration.SubnetId, ignoreCase: true);
        Assert.Equal(publicIpId, ipConfiguration.PublicIPAddress?.Id, ignoreCase: true);
        Assert.True(nic.EnableIPForwarding);
        Assert.True(nic.EnableAcceleratedNetworking);
        Assert.Equal("test", nic.DnsSettingsInternalDnsNameLabel);
        Assert.Equal("100.1.2.3", Assert.Single(nic.DnsSettingsDnsServers ?? []));
        AssertTag(nic, "foo", "doo");

        // Step 8: a full replace, with the security group by name.
        nic = Assert.IsType<NetworkInterface>(Assert.Single(run(new Step(
            ["network", "nic", "create", "--subscription-id", subscriptionId, "--name", n.Nic, "--resource-group", n.Group, "--subnet", n.Subnet,
             "--vnet-name", n.VNet, "--network-security-group", n.Nsg],
            "New-AzNetworkInterface",
            new()
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = n.Nic,
                ["ResourceGroupName"] = n.Group,
                ["Subnet"] = n.Subnet,
                ["VirtualNetworkName"] = n.VNet,
                ["NetworkSecurityGroup"] = n.Nsg,
            }))));
        ipConfiguration = Assert.Single(nic.IpConfigurations ?? []);
        Assert.Equal("Dynamic", ipConfiguration.PrivateIPAllocationMethod);
        Assert.False(nic.EnableIPForwarding);
        Assert.Equal(nsgId, nic.NetworkSecurityGroupId, ignoreCase: true);
        Assert.Null(ipConfiguration.PublicIPAddress);

        // Step 9: the security group and public IP by ID.
        nic = Assert.IsType<NetworkInterface>(Assert.Single(run(new Step(
            ["network", "nic", "create", "--subscription-id", subscriptionId, "--name", n.Nic, "--resource-group", n.Group, "--subnet", n.Subnet,
             "--vnet-name", n.VNet, "--network-security-group", secondNsgId, "--public-ip-address", publicIpId],
            "New-AzNetworkInterface",
            new()
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = n.Nic,
                ["ResourceGroupName"] = n.Group,
                ["Subnet"] = n.Subnet,
                ["VirtualNetworkName"] = n.VNet,
                ["NetworkSecurityGroup"] = secondNsgId,
                ["PublicIpAddress"] = publicIpId,
            }))));
        Assert.Equal(secondNsgId, nic.NetworkSecurityGroupId, ignoreCase: true);
        Assert.Equal(publicIpId, Assert.Single(nic.IpConfigurations ?? []).PublicIPAddress?.Id, ignoreCase: true);

        // Step 10: an explicit location skips the resource group lookup.
        (outputs, sent) = Capture(interceptor, () => run(new Step(
            ["network", "nic", "create", "--subscription-id", subscriptionId, "--name", n.Nic2, "--resource-group", n.Group, "--location", Location,
             "--subnet", subnetId, "--ip-config-name", "createipconf", "--private-ip-address-version", "IPv4"],
            "New-AzNetworkInterface",
            new()
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = n.Nic2,
                ["ResourceGroupName"] = n.Group,
                ["Location"] = Location,
                ["Subnet"] = subnetId,
                ["IpConfigurationName"] = "createipconf",
                ["PrivateIpAddressVersion"] = "IPv4",
            })));
        AssertGroupLookupBeforePut(sent, n.Group, n.Nic2, expected: false);
        nic = Assert.IsType<NetworkInterface>(Assert.Single(outputs));
        Assert.Equal(Location, nic.Location);
        ipConfiguration = Assert.Single(nic.IpConfigurations ?? []);
        Assert.Equal("createipconf", ipConfiguration.Name);
        Assert.True(ipConfiguration.Primary);
        Assert.Equal("Dynamic", ipConfiguration.PrivateIPAllocationMethod);
        Assert.Equal("IPv4", ipConfiguration.PrivateIPAddressVersion);
        Assert.False(nic.EnableIPForwarding);
        Assert.False(nic.EnableAcceleratedNetworking);

        // Step 11: show returns the public IP as a reference only.
        var show = new Step(
            ["network", "nic", "show", "--subscription-id", subscriptionId, "--name", n.Nic, "--resource-group", n.Group],
            "Get-AzNetworkInterface_Get",
            new() { ["SubscriptionId"] = subscriptionId, ["Name"] = n.Nic, ["ResourceGroupName"] = n.Group });
        nic = Assert.IsType<NetworkInterface>(Assert.Single(run(show)));
        Assert.Equal(n.Nic, nic.Name);
        ipConfiguration = Assert.Single(nic.IpConfigurations ?? []);
        Assert.Equal(publicIpId, ipConfiguration.PublicIPAddress?.Id, ignoreCase: true);
        Assert.Null(ipConfiguration.PublicIPAddress?.Name);

        // Step 12: $expand inlines the public IP.
        (outputs, sent) = Capture(interceptor, () => run(new Step(
            ["network", "nic", "show", "--subscription-id", subscriptionId, "--name", n.Nic, "--resource-group", n.Group,
             "--expand", "ipConfigurations/publicIPAddress"],
            "Get-AzNetworkInterface_Get",
            new()
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = n.Nic,
                ["ResourceGroupName"] = n.Group,
                ["ExpandResource"] = "ipConfigurations/publicIPAddress",
            })));
        var expandRequest = Assert.Single(sent, request => request.Method == "GET");
        Assert.Contains("$expand=ipConfigurations/publicIPAddress", Uri.UnescapeDataString(expandRequest.Uri), StringComparison.Ordinal);
        nic = Assert.IsType<NetworkInterface>(Assert.Single(outputs));
        Assert.Equal(n.PublicIp, Assert.Single(nic.IpConfigurations ?? []).PublicIPAddress?.Name);

        // Step 13: resource group list.
        var groupList = new Step(
            ["network", "nic", "list", "--subscription-id", subscriptionId, "--resource-group", n.Group],
            "Get-AzNetworkInterface_List",
            new() { ["SubscriptionId"] = subscriptionId, ["ResourceGroupName"] = n.Group });
        (outputs, sent) = Capture(interceptor, () => run(groupList));
        Assert.EndsWith(
            $"/resourceGroups/{n.Group}/providers/Microsoft.Network/networkInterfaces",
            new Uri(sent[0].Uri).AbsolutePath,
            StringComparison.OrdinalIgnoreCase);
        var listed = Assert.IsType<List<NetworkInterface>>(Assert.Single(outputs));
        Assert.Equal(
            new[] { n.Nic, n.Nic2 }.Order(StringComparer.Ordinal),
            listed.Select(item => item.Name).Order(StringComparer.Ordinal));

        // Step 14: subscription list.
        (outputs, sent) = Capture(interceptor, () => run(new Step(
            ["network", "nic", "list", "--subscription-id", subscriptionId],
            "Get-AzNetworkInterface_List",
            new() { ["SubscriptionId"] = subscriptionId })));
        var subscriptionListPath = new Uri(sent[0].Uri).AbsolutePath;
        Assert.EndsWith("/providers/Microsoft.Network/networkInterfaces", subscriptionListPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/resourceGroups/", subscriptionListPath, StringComparison.OrdinalIgnoreCase);
        var listedNames = Assert.IsType<List<NetworkInterface>>(Assert.Single(outputs)).Select(item => item.Name).ToList();
        Assert.Contains(n.Nic, listedNames);
        Assert.Contains(n.Nic2, listedNames);

        // Steps 15 and 16: delete writes nothing.
        Assert.Empty(run(new Step(
            ["network", "nic", "delete", "--subscription-id", subscriptionId, "--name", n.Nic, "--resource-group", n.Group],
            "Remove-AzNetworkInterface",
            new() { ["SubscriptionId"] = subscriptionId, ["Name"] = n.Nic, ["ResourceGroupName"] = n.Group })));
        Assert.Empty(run(new Step(
            ["network", "nic", "delete", "--subscription-id", subscriptionId, "--name", n.Nic2, "--resource-group", n.Group],
            "Remove-AzNetworkInterface",
            new() { ["SubscriptionId"] = subscriptionId, ["Name"] = n.Nic2, ["ResourceGroupName"] = n.Group })));

        // Step 17: the deleted interface is a service 404.
        AssertResourceNotFound(() => run(show));

        // Step 18: the resource group list is empty.
        AssertEmptyList(run(groupList));
    }

    /// <summary>One step in its CLI form and its PowerShell form.</summary>
    private sealed record Step(string[] Cli, string PsCommand, Dictionary<string, object> Ps, string PsExtension = "Network");

    /// <summary>The T1 resource names, run-scoped in record mode.</summary>
    private sealed record LifecycleNames(string Group, string VNet, string Subnet, string PublicIp, string Nsg, string Nsg2, string Nic, string Nic2)
    {
        public static LifecycleNames For(RecorderMode mode) => new(
            TestRun.ScopedName(GroupBase, mode),
            TestRun.ScopedName(VNetBase, mode),
            TestRun.ScopedName(SubnetBase, mode),
            TestRun.ScopedName(PublicIpBase, mode),
            TestRun.ScopedName(NsgBase, mode),
            TestRun.ScopedName(SecondNsgBase, mode),
            TestRun.ScopedName(NicBase, mode),
            TestRun.ScopedName(SecondNicBase, mode));
    }

    /// <summary>Runs a step and returns its outputs with the requests it sent, in send order.</summary>
    private static (IReadOnlyList<object> Outputs, List<CapturedHttpRequest> Sent) Capture(RecordPlaybackInterceptor interceptor, Func<IReadOnlyList<object>> run)
    {
        int before = interceptor.Sent.Count;
        var outputs = run();
        return (outputs, interceptor.Sent.Skip(before).ToList());
    }

    /// <summary>Asserts whether a GET of the resource group precedes the network interface PUT.</summary>
    private static void AssertGroupLookupBeforePut(List<CapturedHttpRequest> sent, string groupName, string nicName, bool expected)
    {
        int put = sent.FindIndex(request => request.Method == "PUT"
            && request.Uri.Contains($"/networkInterfaces/{nicName}?", StringComparison.OrdinalIgnoreCase));
        Assert.True(put >= 0, $"no PUT was sent for network interface '{nicName}'");

        bool lookup = sent.Take(put).Any(request => request.Method == "GET"
            && request.Uri.Contains($"/resourcegroups/{groupName}?", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expected, lookup);
    }

    /// <summary>Asserts a create returned an ID, which later steps pass back in.</summary>
    private static string RequireId(string? id)
    {
        Assert.NotNull(id);
        return id;
    }

    private static void AssertTag(NetworkInterface nic, string key, string value)
    {
        Assert.NotNull(nic.Tags);
        Assert.True(nic.Tags.TryGetValue(key, out var actual), $"tag '{key}' is missing");
        Assert.Equal(value, actual);
    }

    /// <summary>A list command writes one list; an empty result is an empty list or no output.</summary>
    private static void AssertEmptyList(IReadOnlyList<object> outputs)
    {
        Assert.True(outputs.Count <= 1, $"expected at most one list output, got {outputs.Count}");
        foreach (var output in outputs)
        {
            Assert.Empty(Assert.IsType<List<NetworkInterface>>(output));
        }
    }

    private static void AssertResourceNotFound(System.Action action)
    {
        var error = Record.Exception(action);
        Assert.NotNull(error);

        var text = error.ToString();
        Assert.Contains("ResourceNotFound", text);
        if (FindHttpError(error) is { } http)
        {
            Assert.Equal(HttpStatusCode.NotFound, http.StatusCode);
        }
        else
        {
            Assert.Contains("404", text);
        }
    }

    private static HttpRequestException? FindHttpError(Exception? error) => error switch
    {
        null => null,
        HttpRequestException http => http,
        AggregateException aggregate => aggregate.InnerExceptions.Select(FindHttpError).FirstOrDefault(found => found is not null),
        _ => FindHttpError(error.InnerException),
    };

    private static List<object> Invoke(Runspace runspace, string command, Dictionary<string, object> parameters)
    {
        using var powerShell = PowerShell.Create();
        powerShell.Runspace = runspace;
        var outputs = powerShell.AddCommand(command).AddParameters(parameters).Invoke();

        Assert.Empty(powerShell.Streams.Error);
        return outputs.Select(output => output.BaseObject).ToList();
    }

    /// <summary>
    /// Trims a network interface list response to the interfaces this scenario created. The
    /// subscription-scope list otherwise records every interface in the recording subscription.
    /// Runs after the default sanitizers, so it sees base names.
    /// </summary>
    /// <remarks>
    /// Only a JSON object whose <c>value</c> array holds network interfaces is touched, so URIs,
    /// headers and other bodies pass through unchanged, and a second pass removes nothing.
    /// </remarks>
    private sealed class NetworkInterfaceListSanitizer(string resourceGroupName, string[] keptNames) : IRecordingSanitizer
    {
        private const string NetworkInterfaceType = "Microsoft.Network/networkInterfaces";

        public string Name => "network-interface-list";

        public string Sanitize(string value)
        {
            if (!value.Contains("\"value\"", StringComparison.Ordinal)
                || !value.Contains(NetworkInterfaceType, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            JsonObject? root;
            try
            {
                root = JsonNode.Parse(value) as JsonObject;
            }
            catch (JsonException)
            {
                return value;
            }

            if (root?["value"] is not JsonArray items)
            {
                return value;
            }

            var foreign = items.Where(item => IsNetworkInterface(item) && !IsKept(item)).ToList();
            if (foreign.Count == 0)
            {
                return value;
            }

            foreach (var item in foreign)
            {
                items.Remove(item);
            }

            return root.ToJsonString();
        }

        private static bool IsNetworkInterface(JsonNode? item) =>
            item is JsonObject resource
            && resource["type"] is JsonValue type
            && type.TryGetValue<string>(out var typeName)
            && string.Equals(typeName, NetworkInterfaceType, StringComparison.OrdinalIgnoreCase);

        private bool IsKept(JsonNode? item) =>
            item is JsonObject resource
            && resource["id"] is JsonValue id
            && id.TryGetValue<string>(out var resourceId)
            && resourceId.Contains($"/resourceGroups/{resourceGroupName}/", StringComparison.OrdinalIgnoreCase)
            && resource["name"] is JsonValue name
            && name.TryGetValue<string>(out var resourceName)
            && keptNames.Contains(resourceName, StringComparer.OrdinalIgnoreCase);
    }
}
