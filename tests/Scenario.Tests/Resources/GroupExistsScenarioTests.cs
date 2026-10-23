using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// Records both outcomes of <c>az group exists</c> against a real resource group. The resource
/// group is deleted within the recorded workflow so the live run does not leave resources behind.
/// </summary>
[Collection("Scenario Tests")]
public class GroupExistsScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-group-exists-rg";
    private const string Location = "westus2";

    [Fact]
    public void ReportsExistingAndDeletedResourceGroup()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(GroupExistsScenarioTests),
            nameof(ReportsExistingAndDeletedResourceGroup));

        if (recording.Mode == RecorderMode.Record)
        {
            var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                useLiveAuth: true);
            var subscriptionId = host.LiveSubscriptionId();
            try
            {
                RunWorkflow(host, subscriptionId, groupName);
                recording.Save();
            }
            finally
            {
                // Catches a failure before the workflow's own delete; runs after the save.
                try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
            }
            return;
        }

        using var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor());
        RunWorkflow(cli, SubscriptionId);
    }

    private static void RunWorkflow(ScenarioHost host, string subscriptionId, string? groupName = null)
    {
        var name = groupName ?? ResourceGroupName;
        IReadOnlyList<object> created = host.RunCli(
            "group",
            "create",
            "--subscription-id",
            subscriptionId,
            "--name",
            name,
            "--location",
            Location);
        Assert.Equal(name, Assert.IsType<ResourceGroup>(Assert.Single(created)).Name);

        AssertExists(
            host.RunCli(
                "group",
                "exists",
                "--subscription-id",
                subscriptionId,
                "--name",
                name),
            expected: true);

        Assert.Empty(host.RunCli(
            "group",
            "delete",
            "--subscription-id",
            subscriptionId,
            "--name",
            name));

        AssertExists(
            host.RunCli(
                "group",
                "exists",
                "--subscription-id",
                subscriptionId,
                "--name",
                name),
            expected: false);
    }

    private static void AssertExists(IReadOnlyList<object> outputs, bool expected) =>
        Assert.Equal(expected, Assert.IsType<bool>(Assert.Single(outputs)));
}
