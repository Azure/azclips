// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
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
    public void CliLifecycleAndPowerShellCreate()
    {
        using (var cli = new ScenarioHost(
            ClientType.CLI,
            new PassThroughInterceptor(),
            new FakeNsgArmBackend(SubscriptionId),
            extensions: [ExtensionUnderTest.Network]))
        {
            AssertNsg(Assert.Single(cli.RunCli(CreateArgs())));
            AssertNsg(Assert.Single(cli.RunCli(ShowArgs())));

            var listed = Assert.IsType<List<NetworkSecurityGroup>>(Assert.Single(cli.RunCli(ListArgs())));
            Assert.Collection(
                listed,
                first => Assert.Equal(NsgName, first.Name),
                second => Assert.Equal("second-nsg", second.Name));

            cli.RunCli(DeleteArgs());
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
    }

    [Fact]
    public void PowerShellCmdlet_ConvertsHashtableTags()
    {
        var backend = new FakeNsgArmBackend(SubscriptionId);
        using var host = new ScenarioHost(
            ClientType.PowerShell,
            new PassThroughInterceptor(),
            backend,
            extensions: [ExtensionUnderTest.Network]);

        var sessionState = InitialSessionState.CreateDefault2();
        sessionState.Commands.Add(new SessionStateCmdletEntry(
            "New-AzNetworkSecurityGroup",
            typeof(Microsoft.Azure.Azclips.Cmdlets.Network.NewNetworkSecurityGroupCmdlet),
            null));
        using var runspace = RunspaceFactory.CreateRunspace(sessionState);
        runspace.Open();
        using var powerShell = PowerShell.Create();
        powerShell.Runspace = runspace;
        powerShell
            .AddCommand("New-AzNetworkSecurityGroup")
            .AddParameter("SubscriptionId", SubscriptionId)
            .AddParameter("ResourceGroupName", ResourceGroupName)
            .AddParameter("Name", NsgName)
            .AddParameter("Location", "eastus")
            .AddParameter("Tag", new Hashtable { ["Owner"] = "PowerShell" });

        var outputs = powerShell.Invoke();

        Assert.Empty(powerShell.Streams.Error);
        Assert.IsType<NetworkSecurityGroup>(Assert.Single(outputs).BaseObject);
        Assert.Equal("PowerShell", backend.LastCreateTags?["Owner"]);
    }

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

    private static void AssertNsg(object output)
    {
        var nsg = Assert.IsType<NetworkSecurityGroup>(output);
        Assert.Equal(NsgName, nsg.Name);
        Assert.Equal("eastus", nsg.Location);
        Assert.Equal("scenario", nsg.Tags?["owner"]);
        Assert.Equal("Succeeded", nsg.Properties.ProvisioningState);
        Assert.Single(nsg.Properties.DefaultSecurityRules ?? []);
    }

    private sealed class PassThroughInterceptor : ITracingInterceptor
    {
        public SendAsyncStep SendAsync => (request, publisher, next) => next.SendAsync(request, publisher);
    }
}
