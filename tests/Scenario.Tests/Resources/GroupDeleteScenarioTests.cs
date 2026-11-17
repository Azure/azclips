using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// Creates and deletes a resource group through the real dispatcher, binders, and ARM pipeline.
/// Record mode captures the CLI workflow against Azure; playback replays the same traffic through
/// both the CLI and PowerShell surfaces.
/// </summary>
[Collection("Scenario Tests")]
public class GroupDeleteScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-group-delete-rg";
    private const string Location = "westus2";

    [Fact]
    public void CreatesAndDeletesResourceGroup()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(GroupDeleteScenarioTests),
            nameof(CreatesAndDeletesResourceGroup));

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                useLiveAuth: true);
            string subscriptionId = host.LiveSubscriptionId();

            try
            {
                AssertCreated(RunCliCreate(host, subscriptionId, groupName), groupName);
                Assert.Empty(RunCliDelete(host, subscriptionId, groupName));
                recording.Save();
            }
            finally
            {
                // Catches a failure before the scenario's own delete; runs after the save.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
            }
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor()))
        {
            AssertCreated(RunCliCreate(cli, SubscriptionId));
            Assert.Empty(RunCliDelete(cli, SubscriptionId));
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor()))
        {
            AssertCreated(RunPowerShellCreate(ps, SubscriptionId));
            Assert.Empty(RunPowerShellDelete(ps, SubscriptionId));
        }
    }

    private static IReadOnlyList<object> RunCliCreate(ScenarioHost host, string subscriptionId, string? groupName = null) =>
        host.RunCli(
            "group",
            "create",
            "--subscription-id",
            subscriptionId,
            "--name",
            groupName ?? ResourceGroupName,
            "--location",
            Location);

    private static IReadOnlyList<object> RunCliDelete(ScenarioHost host, string subscriptionId, string? groupName = null) =>
        host.RunCli(
            "group",
            "delete",
            "--subscription-id",
            subscriptionId,
            "--name",
            groupName ?? ResourceGroupName);

    private static IReadOnlyList<object> RunPowerShellCreate(ScenarioHost host, string subscriptionId) =>
        host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
        {
            ["SubscriptionId"] = subscriptionId,
            ["Name"] = ResourceGroupName,
            ["Location"] = Location,
        });

    private static IReadOnlyList<object> RunPowerShellDelete(ScenarioHost host, string subscriptionId) =>
        host.RunPowerShell("Remove-AzResourceGroup", new Dictionary<string, object>
        {
            ["SubscriptionId"] = subscriptionId,
            ["Name"] = ResourceGroupName,
        });

    private static void AssertCreated(IReadOnlyList<object> outputs, string? groupName = null)
    {
        var resourceGroup = Assert.IsType<ResourceGroup>(Assert.Single(outputs));
        Assert.Equal(groupName ?? ResourceGroupName, resourceGroup.Name);
        Assert.Equal(Location, resourceGroup.Location);
        Assert.Equal("Succeeded", resourceGroup.Properties?.ProvisioningState);
    }
}
