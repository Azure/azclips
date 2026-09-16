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
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                useLiveAuth: true);
            RunWorkflow(host, host.LiveSubscriptionId());
            recording.Save();
            return;
        }

        using var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor());
        RunWorkflow(cli, SubscriptionId);
    }

    private static void RunWorkflow(ScenarioHost host, string subscriptionId)
    {
        IReadOnlyList<object> created = host.RunCli(
            "group",
            "create",
            "--subscription-id",
            subscriptionId,
            "--name",
            ResourceGroupName,
            "--location",
            Location);
        Assert.Equal(ResourceGroupName, Assert.IsType<ResourceGroup>(Assert.Single(created)).Name);

        AssertExists(
            host.RunCli(
                "group",
                "exists",
                "--subscription-id",
                subscriptionId,
                "--name",
                ResourceGroupName),
            expected: true);

        Assert.Empty(host.RunCli(
            "group",
            "delete",
            "--subscription-id",
            subscriptionId,
            "--name",
            ResourceGroupName,
            "--yes"));

        AssertExists(
            host.RunCli(
                "group",
                "exists",
                "--subscription-id",
                subscriptionId,
                "--name",
                ResourceGroupName),
            expected: false);
    }

    private static void AssertExists(IReadOnlyList<object> outputs, bool expected) =>
        Assert.Equal(expected, Assert.IsType<bool>(Assert.Single(outputs)));
}
