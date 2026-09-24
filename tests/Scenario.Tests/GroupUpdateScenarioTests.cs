using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Records the CLI and PowerShell resource-group update workflows against Azure. Both surfaces
/// share resource creation, deletion, and result assertions while retaining their client-specific
/// update shapes.
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
            RunCliWorkflow(host, host.LiveSubscriptionId());
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor()))
        {
            RunCliWorkflow(cli, SubscriptionId);
        }

        using (var powerShell = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor()))
        {
            RunPowerShellWorkflow(powerShell, SubscriptionId);
        }
    }

    private static void RunCliWorkflow(ScenarioHost host, string subscriptionId) =>
        RunWorkflow(host, subscriptionId, ClientType.CLI, () =>
        {
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
                    "--tags",
                    "Owner=PowerShell"),
                new Dictionary<string, string>
                {
                    ["Owner"] = "PowerShell",
                });
        });

    private static void RunPowerShellWorkflow(ScenarioHost host, string subscriptionId) =>
        RunWorkflow(host, subscriptionId, ClientType.PowerShell, () =>
        {
            AssertResourceGroup(
                host.RunPowerShell("Set-AzResourceGroup", new Dictionary<string, object>
                {
                    ["SubscriptionId"] = subscriptionId,
                    ["Name"] = ResourceGroupName,
                    ["Tag"] = new Dictionary<string, string>
                    {
                        ["Environment"] = "Scenario",
                        ["Owner"] = "AzClips",
                    },
                }),
                new Dictionary<string, string>
                {
                    ["Environment"] = "Scenario",
                    ["Owner"] = "AzClips",
                });

            AssertResourceGroup(
                host.RunPowerShell("Set-AzResourceGroup", new Dictionary<string, object>
                {
                    ["Id"] = $"/subscriptions/{subscriptionId}/resourceGroups/{ResourceGroupName}",
                    ["Tag"] = new Dictionary<string, string>
                    {
                        ["Owner"] = "PowerShell",
                    },
                }),
                new Dictionary<string, string>
                {
                    ["Owner"] = "PowerShell",
                });
        });

    private static void RunWorkflow(
        ScenarioHost host,
        string subscriptionId,
        ClientType clientType,
        Action updateResourceGroup)
    {
        try
        {
            AssertResourceGroup(
                CreateResourceGroup(host, subscriptionId, clientType),
                expectedTags: null);
            updateResourceGroup();
        }
        catch (Exception workflowException)
        {
            try
            {
                DeleteResourceGroup(host, subscriptionId, clientType);
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

        DeleteResourceGroup(host, subscriptionId, clientType);
    }

    private static IReadOnlyList<object> CreateResourceGroup(
        ScenarioHost host,
        string subscriptionId,
        ClientType clientType) =>
        clientType switch
        {
            ClientType.CLI => host.RunCli(
                "group",
                "create",
                "--subscription-id",
                subscriptionId,
                "--name",
                ResourceGroupName,
                "--location",
                Location),
            ClientType.PowerShell => host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = ResourceGroupName,
                ["Location"] = Location,
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(clientType)),
        };

    private static void DeleteResourceGroup(
        ScenarioHost host,
        string subscriptionId,
        ClientType clientType)
    {
        IReadOnlyList<object> outputs = clientType switch
        {
            ClientType.CLI => host.RunCli(
                "group",
                "delete",
                "--subscription-id",
                subscriptionId,
                "--name",
                ResourceGroupName),
            ClientType.PowerShell => host.RunPowerShell("Remove-AzResourceGroup", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = ResourceGroupName,
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(clientType)),
        };

        Assert.Empty(outputs);
    }

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
