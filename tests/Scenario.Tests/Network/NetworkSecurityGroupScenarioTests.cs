// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.Cmdlets.Network;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Tests.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class NetworkSecurityGroupScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-nsg-rg";
    private const string NsgName = "scenario-nsg";

    [Fact]
    public void CliAndPowerShellLifecycle()
    {
        using (var cli = new ScenarioHost(
            ClientType.CLI,
            new PassThroughInterceptor(),
            new FakeNsgArmBackend(SubscriptionId),
            extensions: [ExtensionUnderTest.Network]))
        {
            AssertNsg(Assert.Single(cli.RunCli(CreateArgs())));
            AssertNsg(Assert.Single(cli.RunCli(ShowArgs())));

            AssertListed(Assert.Single(cli.RunCli(ListArgs())));

            // The delete finishes on the network RP's empty 204 operation result, so it prints nothing.
            Assert.Empty(cli.RunCli(DeleteArgs()));
        }

        using var ps = new ScenarioHost(
            ClientType.PowerShell,
            new PassThroughInterceptor(),
            new FakeNsgArmBackend(SubscriptionId),
            extensions: [ExtensionUnderTest.Network]);
        AssertNsg(Assert.Single(ps.RunPowerShell(
            "New-AzNetworkSecurityGroup",
            new Dictionary<string, object>
            {
                ["SubscriptionId"] = SubscriptionId,
                ["ResourceGroupName"] = ResourceGroupName,
                ["Name"] = NsgName,
                ["Location"] = "eastus",
                ["Tag"] = new List<string> { "owner=scenario" },
            },
            extension: "Network")));
        AssertNsg(Assert.Single(ps.RunPowerShell("Get-AzNetworkSecurityGroup_Get", PsResourceParameters(), extension: "Network")));
        AssertListed(Assert.Single(ps.RunPowerShell(
            "Get-AzNetworkSecurityGroup_List",
            new Dictionary<string, object> { ["SubscriptionId"] = SubscriptionId },
            extension: "Network")));
        Assert.Empty(ps.RunPowerShell("Remove-AzNetworkSecurityGroup", PsResourceParameters(), extension: "Network"));
    }

    /// <summary>
    /// Runs the real cmdlet classes in a runspace, the layer <c>ScenarioHost.RunPowerShell</c>
    /// bypasses: the hashtable tag must convert into the tokens the processor binds, Get must route
    /// by parameter set and enumerate every page of the list, and Remove's -Force must skip the
    /// High-impact prompt that a host without UI cannot answer.
    /// </summary>
    [Fact]
    public void PowerShellCmdlets_ConvertTheirParameters_AndRouteParameterSets()
    {
        var backend = new FakeNsgArmBackend(SubscriptionId);
        using var host = new ScenarioHost(
            ClientType.PowerShell,
            new PassThroughInterceptor(),
            backend,
            extensions: [ExtensionUnderTest.Network]);

        var sessionState = InitialSessionState.CreateDefault2();
        sessionState.Commands.Add(new SessionStateCmdletEntry("New-AzNetworkSecurityGroup", typeof(NewNetworkSecurityGroupCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Get-AzNetworkSecurityGroup", typeof(GetNetworkSecurityGroupCmdlet), null));
        sessionState.Commands.Add(new SessionStateCmdletEntry("Remove-AzNetworkSecurityGroup", typeof(RemoveNetworkSecurityGroupCmdlet), null));
        using var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();

        var created = Invoke(runspace, "New-AzNetworkSecurityGroup", new(PsResourceParameters())
        {
            ["Location"] = "eastus",
            ["Tag"] = new Hashtable { ["Owner"] = "PowerShell" },
        });
        Assert.IsType<NetworkSecurityGroup>(Assert.Single(created));
        Assert.Equal("PowerShell", backend.LastCreateTags?["Owner"]);

        // List is the default parameter set and enumerates both pages into individual objects.
        var listed = Invoke(runspace, "Get-AzNetworkSecurityGroup", new() { ["SubscriptionId"] = SubscriptionId });
        Assert.Equal([NsgName, "second-nsg"], listed.Cast<NetworkSecurityGroup>().Select(nsg => nsg.Name!).ToArray());
        AssertNsg(Assert.Single(Invoke(runspace, "Get-AzNetworkSecurityGroup", PsResourceParameters())));

        // The delete itself writes nothing, so -PassThru's true is the only output.
        var removed = Invoke(runspace, "Remove-AzNetworkSecurityGroup", new(PsResourceParameters()) { ["Force"] = true, ["PassThru"] = true });
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

    private static Dictionary<string, object> PsResourceParameters() => new()
    {
        ["SubscriptionId"] = SubscriptionId,
        ["ResourceGroupName"] = ResourceGroupName,
        ["Name"] = NsgName,
    };

    private static string[] CreateArgs() =>
    [
        "network", "nsg", "create",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--name", NsgName,
        "--tags", "owner=scenario",
    ];

    private static string[] ShowArgs() =>
    [
        "network", "nsg", "show",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--name", NsgName,
    ];

    private static string[] ListArgs() =>
    [
        "network", "nsg", "list",
        "--subscription-id", SubscriptionId,
    ];

    private static string[] DeleteArgs() =>
    [
        "network", "nsg", "delete",
        "--subscription-id", SubscriptionId,
        "--resource-group", ResourceGroupName,
        "--name", NsgName,
    ];

    private static void AssertListed(object output)
    {
        var listed = Assert.IsType<List<NetworkSecurityGroup>>(output);
        Assert.Equal([NsgName, "second-nsg"], listed.Select(nsg => nsg.Name!).ToArray());
    }

    private static void AssertNsg(object output)
    {
        var nsg = Assert.IsType<NetworkSecurityGroup>(output);
        Assert.Equal(NsgName, nsg.Name);
        Assert.Equal("eastus", nsg.Location);
        Assert.Equal("scenario", nsg.Tags?["owner"]);
        Assert.Equal("Succeeded", nsg.ProvisioningState);
        Assert.Single(nsg.DefaultSecurityRules ?? []);
    }

    private sealed class PassThroughInterceptor : ITracingInterceptor
    {
        public SendAsyncStep SendAsync => (request, publisher, next) => next.SendAsync(request, publisher);
    }
}
