// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.Cmdlets.Network;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class SubnetScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-subnet-rg";
    private const string VirtualNetworkName = "scenario-vnet";
    private const string SubnetName = "scenario-subnet";
    private const string NsgName = "scenario-subnet-nsg";
    private const string Location = "westus2";

    /// <summary>
    /// Live lifecycle against the network RP. Record mode runs the CLI against Azure and rewrites the
    /// recording; playback replays it through both surfaces. The create passes the dotted delegation
    /// form and an NSG by name, so the recording proves the RP accepts what the customization sends.
    /// </summary>
    [Fact]
    public void CreateShowListDeleteSubnet()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(SubnetScenarioTests), nameof(CreateShowListDeleteSubnet));

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
                RunCliLifecycle(host, subscriptionId, groupName);
                recording.Save();
            }
            finally
            {
                // After the save, so the cleanup never lands in the recording.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
            }
            return;
        }

        using (var cli = new ScenarioHost(
            ClientType.CLI,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]))
        {
            RunCliLifecycle(cli, SubscriptionId, ResourceGroupName);
        }

        // PowerShell has no ARM subnet create (New-AzVirtualNetworkSubnetConfig is offline), so it
        // replays the prerequisites and then reads and removes the recorded subnet.
        using var ps = new ScenarioHost(
            ClientType.PowerShell,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.Network]);
        ps.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object> { ["SubscriptionId"] = SubscriptionId, ["Name"] = ResourceGroupName, ["Location"] = Location }, extension: "Resources");
        ps.RunPowerShell("New-AzNetworkSecurityGroup", new Dictionary<string, object>(PsParent()) { ["Name"] = NsgName, ["Location"] = Location }, extension: "Network");
        ps.RunPowerShell("New-AzVirtualNetwork", new Dictionary<string, object>(PsParent())
        {
            ["Name"] = VirtualNetworkName,
            ["Location"] = Location,
            ["AddressPrefix"] = new List<string> { "10.30.0.0/16" },
        }, extension: "Network");
        AssertLiveSubnet(Assert.Single(ps.RunPowerShell("Get-AzVirtualNetworkSubnetConfig_Get", new Dictionary<string, object>(PsSubnet()) { ["ExpandResource"] = "networkSecurityGroup" }, extension: "Network")), expanded: true);
        AssertLiveSubnet(Assert.Single(Assert.IsType<List<Subnet>>(Assert.Single(ps.RunPowerShell("Get-AzVirtualNetworkSubnetConfig_List", PsVNet(), extension: "Network")))));
        Assert.Empty(ps.RunPowerShell("Remove-AzVirtualNetworkSubnetConfig", PsSubnet(), extension: "Network"));
    }

    private static void RunCliLifecycle(ScenarioHost host, string subscriptionId, string groupName)
    {
        string[] scope = ["--subscription-id", subscriptionId, "--resource-group", groupName];
        string[] subnet = [.. scope, "--vnet-name", VirtualNetworkName, "--name", SubnetName];

        host.RunCli("group", "create", "--subscription-id", subscriptionId, "--name", groupName, "--location", Location);
        host.RunCli(["network", "nsg", "create", .. scope, "--name", NsgName, "--location", Location]);
        host.RunCli(["network", "vnet", "create", .. scope, "--name", VirtualNetworkName, "--location", Location, "--address-prefixes", "10.30.0.0/16"]);

        AssertLiveSubnet(Assert.Single(host.RunCli([
            "network", "vnet", "subnet", "create", .. subnet,
            "--address-prefixes", "10.30.1.0/24",
            "--nsg", NsgName,
            "--service-endpoints", "Microsoft.Storage",
            "--delegations", "Microsoft.Web.serverFarms",
            "--default-outbound-access", "false"])));
        AssertLiveSubnet(Assert.Single(host.RunCli(["network", "vnet", "subnet", "show", .. subnet, "--expand", "networkSecurityGroup"])), expanded: true);
        AssertLiveSubnet(Assert.Single(Assert.IsType<List<Subnet>>(Assert.Single(host.RunCli([
            "network", "vnet", "subnet", "list", .. scope, "--vnet-name", VirtualNetworkName])))));

        // The RP finishes the delete on an empty 204 operation result, so it prints nothing.
        Assert.Empty(host.RunCli(["network", "vnet", "subnet", "delete", .. subnet]));
    }

    private static void AssertLiveSubnet(object output, bool expanded = false)
    {
        var subnet = Assert.IsType<Subnet>(output);
        Assert.Equal(SubnetName, subnet.Name);
        Assert.Equal("Succeeded", subnet.Properties.ProvisioningState);
        Assert.Equal("10.30.1.0/24", subnet.Properties.AddressPrefix ?? Assert.Single(subnet.Properties.AddressPrefixes ?? []));
        Assert.EndsWith($"/networkSecurityGroups/{NsgName}", subnet.Properties.NetworkSecurityGroup?.Id, StringComparison.OrdinalIgnoreCase);
        // $expand returns the whole NSG; the reference must keep it rather than collapse to the id.
        Assert.Equal(expanded, subnet.Properties.NetworkSecurityGroup?.AdditionalProperties?.ContainsKey("location") == true);
        Assert.Equal("Microsoft.Storage", Assert.Single(subnet.Properties.ServiceEndpoints).Service);
        Assert.Equal("Microsoft.Web/serverFarms", Assert.Single(subnet.Properties.Delegations).Properties.ServiceName);
        Assert.False(subnet.Properties.DefaultOutboundAccess);
    }

    private static Dictionary<string, object> PsParent() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
    };

    private static Dictionary<string, object> PsVNet() => new Dictionary<string, object>(PsParent()) { ["VirtualNetworkName"] = VirtualNetworkName };

    private static Dictionary<string, object> PsSubnet() => new Dictionary<string, object>(PsVNet()) { ["Name"] = SubnetName };

    [Fact]
    public void CliAndPowerShellLifecycle_AgainstRpShapedBackend()
    {
        using (var cli = new ScenarioHost(
            ClientType.CLI,
            new PassThroughInterceptor(),
            new FakeSubnetArmBackend(SubscriptionId, ResourceGroupName, VirtualNetworkName),
            extensions: [ExtensionUnderTest.Network]))
        {
            AssertSubnet(Assert.Single(cli.RunCli(CreateArgs())));
            AssertSubnet(Assert.Single(cli.RunCli(ShowArgs())));
            AssertPaged(Assert.Single(cli.RunCli(ListArgs())));
            Assert.Empty(cli.RunCli(DeleteArgs()));
            Assert.ThrowsAny<Exception>(() => cli.RunCli(ShowArgs()));
        }

        var backend = new FakeSubnetArmBackend(SubscriptionId, ResourceGroupName, VirtualNetworkName);
        using var ps = new ScenarioHost(
            ClientType.PowerShell,
            new PassThroughInterceptor(),
            backend,
            extensions: [ExtensionUnderTest.Network]);
        var config = Assert.Single(ps.RunPowerShell("New-AzVirtualNetworkSubnetConfig", PsConfigParameters(), extension: "Network"));
        ps.RunPowerShell("New-AzVirtualNetwork", new Dictionary<string, object>(PsParent())
        {
            ["Name"] = VirtualNetworkName,
            ["Location"] = Location,
            ["AddressPrefix"] = new List<string> { "10.20.0.0/16" },
            ["Subnet"] = new List<object> { config },
        }, extension: "Network");
        AssertSubnet(Assert.Single(ps.RunPowerShell("Get-AzVirtualNetworkSubnetConfig_Get", PsSubnet(), extension: "Network")));
        AssertPaged(Assert.Single(ps.RunPowerShell("Get-AzVirtualNetworkSubnetConfig_List", PsVNet(), extension: "Network")));
        Assert.Empty(ps.RunPowerShell("Remove-AzVirtualNetworkSubnetConfig", PsSubnet(), extension: "Network"));
        Assert.ThrowsAny<Exception>(() => ps.RunPowerShell("Get-AzVirtualNetworkSubnetConfig_Get", PsSubnet(), extension: "Network"));
    }

    [Fact]
    public void Create_UsesLegacyShape_AndDeleteFinishesOnTheLocationResult()
    {
        var record = new SessionRecord();
        using (var host = new ScenarioHost(
            ClientType.CLI,
            new RecordPlaybackInterceptor(RecorderMode.Record, record),
            new FakeSubnetArmBackend(SubscriptionId, ResourceGroupName, VirtualNetworkName),
            extensions: [ExtensionUnderTest.Network]))
        {
            host.RunCli(CreateArgs());
            host.RunCli(DeleteArgs());
        }

        var create = record.Entries.First(entry => entry.Request.Method == "PUT");
        Assert.Contains($"/virtualNetworks/{VirtualNetworkName}/subnets/{SubnetName}", create.Request.Uri);
        Assert.Contains("api-version=2025-07-01", create.Request.Uri);
        Assert.Contains("\"addressPrefixes\":[\"10.20.1.0/24\"]", create.Request.Body);
        Assert.DoesNotContain("\"addressPrefix\":", create.Request.Body);
        Assert.Contains("\"service\":\"Microsoft.Storage\"", create.Request.Body);
        Assert.Contains("\"serviceName\":\"Microsoft.Web/serverFarms\"", create.Request.Body);
        Assert.Contains("\"defaultOutboundAccess\":false", create.Request.Body);
        Assert.DoesNotContain("networkSecurityGroup", create.Request.Body);
        Assert.Contains("/operations/", record.Entries[record.Entries.IndexOf(create) + 1].Request.Uri);

        var deleteIndex = record.Entries.FindIndex(entry => entry.Request.Method == "DELETE");
        Assert.Collection(
            record.Entries.Skip(deleteIndex + 1),
            entry => Assert.Contains("/operations/", entry.Request.Uri),
            entry => Assert.Contains("/operationResults/", entry.Request.Uri));
    }

    [Fact]
    public void PowerShellConfig_BuildsTheEquivalentOfflineObject()
    {
        using var ps = new ScenarioHost(
            ClientType.PowerShell,
            new RecordPlaybackInterceptor(RecorderMode.Playback, new SessionRecord()),
            extensions: [ExtensionUnderTest.Network]);

        AssertSubnet(Assert.Single(ps.RunPowerShell("New-AzVirtualNetworkSubnetConfig", PsConfigParameters(), extension: "Network")));
    }

    /// <summary>
    /// Runs the real cmdlet classes in a runspace, the layer <c>ScenarioHost.RunPowerShell</c>
    /// bypasses: the config object reaches New-AzVirtualNetwork wrapped in a PSObject, Get routes by
    /// parameter set and enumerates every page, and Remove's -Force skips the High-impact prompt.
    /// </summary>
    [Fact]
    public void PowerShellCmdlets_ComposeSubnetConfig_AndRouteParameterSets()
    {
        using var host = new ScenarioHost(
            ClientType.PowerShell,
            new PassThroughInterceptor(),
            new FakeSubnetArmBackend(SubscriptionId, ResourceGroupName, VirtualNetworkName),
            extensions: [ExtensionUnderTest.Network]);

        var sessionState = InitialSessionState.CreateDefault2();
        sessionState.Commands.Add(new SessionStateCmdletEntry("New-AzVirtualNetworkSubnetConfig", typeof(NewVirtualNetworkSubnetConfigCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("New-AzVirtualNetwork", typeof(NewVirtualNetworkCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Get-AzVirtualNetworkSubnetConfig", typeof(GetVirtualNetworkSubnetConfigCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Remove-AzVirtualNetworkSubnetConfig", typeof(RemoveVirtualNetworkSubnetConfigCmdlet), null));
        using var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();

        using (var powerShell = PowerShell.Create())
        {
            powerShell.Runspace = runspace;
            var vnet = powerShell.AddScript($$"""
                $subnet = New-AzVirtualNetworkSubnetConfig -Name {{SubnetName}} -AddressPrefix 10.20.1.0/24 -ServiceEndpoint Microsoft.Storage -DelegatedService Microsoft.Web.serverFarms -DefaultOutboundAccess:$false
                New-AzVirtualNetwork -SubscriptionId {{SubscriptionId}} -ResourceGroupName {{ResourceGroupName}} -Name {{VirtualNetworkName}} -Location {{Location}} -AddressPrefix 10.20.0.0/16 -Subnet $subnet
                """).Invoke();
            Assert.Empty(powerShell.Streams.Error);
            AssertSubnet(Assert.Single(Assert.IsType<VirtualNetwork>(Assert.Single(vnet).BaseObject).Properties.Subnets));
        }

        // List is the default parameter set and enumerates both pages into individual objects.
        var listed = Invoke(runspace, "Get-AzVirtualNetworkSubnetConfig", PsVNet());
        Assert.Equal([SubnetName, $"{SubnetName}-page-2"], listed.Cast<Subnet>().Select(subnet => subnet.Name).ToArray());
        AssertSubnet(Assert.Single(Invoke(runspace, "Get-AzVirtualNetworkSubnetConfig", new Dictionary<string, object>(PsSubnet()) { ["ExpandResource"] = "networkSecurityGroup" })));

        // An inherited $WhatIfPreference must stop -Force from deleting during a dry run.
        using (var powerShell = PowerShell.Create())
        {
            powerShell.Runspace = runspace;
            var output = powerShell.AddScript($"$WhatIfPreference = $true; Remove-AzVirtualNetworkSubnetConfig -SubscriptionId {SubscriptionId} -ResourceGroupName {ResourceGroupName} -VirtualNetworkName {VirtualNetworkName} -Name {SubnetName} -Force -PassThru; $WhatIfPreference = $false").Invoke();
            Assert.Empty(powerShell.Streams.Error);
            Assert.Empty(output);
        }
        AssertSubnet(Assert.Single(Invoke(runspace, "Get-AzVirtualNetworkSubnetConfig", PsSubnet())));

        // The delete itself writes nothing, so -PassThru's true is the only output.
        var removed = Invoke(runspace, "Remove-AzVirtualNetworkSubnetConfig", new Dictionary<string, object>(PsSubnet()) { ["Force"] = true, ["PassThru"] = true });
        Assert.True(Assert.IsType<bool>(Assert.Single(removed)));
    }

    private static List<object> Invoke(Runspace runspace, string command, Dictionary<string, object> parameters)
    {
        using var powerShell = PowerShell.Create();
        powerShell.Runspace = runspace;
        var outputs = powerShell.AddCommand(command).AddParameters(parameters).Invoke();

        Assert.Empty(powerShell.Streams.Error);
        return outputs.Select(output => output.BaseObject).ToList();
    }

    private static Dictionary<string, object> PsConfigParameters() => new()
    {
        ["Name"] = SubnetName,
        ["AddressPrefix"] = new List<string> { "10.20.1.0/24" },
        ["ServiceEndpoint"] = new List<string> { "Microsoft.Storage" },
        ["DelegatedService"] = new List<string> { "Microsoft.Web.serverFarms" },
        ["DefaultOutboundAccess"] = false,
    };

    private static string[] CreateArgs() =>
    [
        "network", "vnet", "subnet", "create",
        "--subscription-id", SubscriptionId,
        "-g", ResourceGroupName,
        "--vnet-name", VirtualNetworkName,
        "-n", SubnetName,
        "--address-prefixes", "10.20.1.0/24",
        "--endpoints", "Microsoft.Storage",
        "--delegations", "Microsoft.Web.serverFarms",
        "--default-outbound", "false"
    ];

    private static string[] ShowArgs() =>
    [
        "network", "vnet", "subnet", "show",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--vnet-name", VirtualNetworkName,
        "--name", SubnetName
    ];

    private static string[] ListArgs() =>
    [
        "network", "vnet", "subnet", "list",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--vnet-name", VirtualNetworkName
    ];

    private static string[] DeleteArgs() =>
    [
        "network", "vnet", "subnet", "delete",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--vnet-name", VirtualNetworkName,
        "--name", SubnetName
    ];

    private static void AssertPaged(object output)
    {
        var listed = Assert.IsType<List<Subnet>>(output);
        Assert.Collection(
            listed,
            subnet => AssertSubnet(subnet),
            subnet => AssertSubnet(subnet, $"{SubnetName}-page-2"));
    }

    private static void AssertSubnet(object output, string expectedName = SubnetName)
    {
        var subnet = Assert.IsType<Subnet>(output);
        Assert.Equal(expectedName, subnet.Name);
        Assert.Equal("10.20.1.0/24", Assert.Single(subnet.Properties.AddressPrefixes ?? []));
        Assert.Equal("Microsoft.Storage", Assert.Single(subnet.Properties.ServiceEndpoints).Service);
        Assert.Equal("Microsoft.Web/serverFarms", Assert.Single(subnet.Properties.Delegations).Properties.ServiceName);
        Assert.False(subnet.Properties.DefaultOutboundAccess);
    }

    private sealed class PassThroughInterceptor : ITracingInterceptor
    {
        public SendAsyncStep SendAsync => (request, publisher, next) => next.SendAsync(request, publisher);
    }
}
