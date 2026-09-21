using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Records the CLI-only resource-group update workflow against Azure. The scenario replaces all
/// tags, applies a generic <c>--set</c> update that preserves a string value, and deletes the group.
/// </summary>
[Collection("Scenario Tests")]
public class GroupUpdateScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-group-update-rg";
    private const string Location = "westus2";

    [Fact]
    public void CreatesUpdatesAndDeletesResourceGroup()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(GroupUpdateScenarioTests),
            nameof(CreatesUpdatesAndDeletesResourceGroup));

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
        try
        {
            AssertResourceGroup(
                host.RunCli(
                    "group",
                    "create",
                    "--subscription-id",
                    subscriptionId,
                    "--name",
                    ResourceGroupName,
                    "--location",
                    Location),
                expectedTags: null);

            AssertResourceGroup(
                host.RunCli(
                    "group",
                    "update",
                    "--subscription-id",
                    subscriptionId,
                    "--resource-group",
                    ResourceGroupName,
                    "--tags",
                    "Environment=Scenario",
                    "Owner=AzClips"),
                new Dictionary<string, string>
                {
                    ["Environment"] = "Scenario",
                    ["Owner"] = "AzClips",
                });

            AssertResourceGroup(
                host.RunCli(
                    "group",
                    "update",
                    "--subscription-id",
                    subscriptionId,
                    "-g",
                    ResourceGroupName,
                    "--set",
                    "tags.CostCenter=042",
                    "--force-string"),
                new Dictionary<string, string>
                {
                    ["Environment"] = "Scenario",
                    ["Owner"] = "AzClips",
                    ["CostCenter"] = "042",
                });
        }
        catch (Exception workflowException)
        {
            try
            {
                DeleteResourceGroup(host, subscriptionId);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "The group update scenario failed and cleanup also failed.",
                    workflowException,
                    cleanupException);
            }

            throw;
        }

        DeleteResourceGroup(host, subscriptionId);
    }

    private static void DeleteResourceGroup(ScenarioHost host, string subscriptionId) =>
        Assert.Empty(host.RunCli(
            "group",
            "delete",
            "--subscription-id",
            subscriptionId,
            "--name",
            ResourceGroupName));

    private static void AssertResourceGroup(
        IReadOnlyList<object> outputs,
        IReadOnlyDictionary<string, string>? expectedTags)
    {
        var resourceGroup = Assert.IsType<ResourceGroup>(Assert.Single(outputs));
        Assert.Equal(ResourceGroupName, resourceGroup.Name);
        Assert.Equal(Location, resourceGroup.Location);

        if (expectedTags is null)
        {
            return;
        }

        Assert.NotNull(resourceGroup.Tags);
        Assert.Equal(expectedTags.Count, resourceGroup.Tags.Count);
        foreach (var tag in expectedTags)
        {
            Assert.True(resourceGroup.Tags.TryGetValue(tag.Key, out string? value));
            Assert.Equal(tag.Value, value);
        }
    }
}
