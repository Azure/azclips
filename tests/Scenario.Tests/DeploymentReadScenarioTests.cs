// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;
using Tests.Common.Utilities;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises the deployment and deployment-operation read commands on both client surfaces. A real
/// resource-group deployment is created first (one network security group, the cheapest resource
/// that still produces deployment operations), then read back by name, by list, and operation by
/// operation. Record mode runs the CLI surface against live Azure and rewrites the recording;
/// playback replays the committed recording without credentials.
/// <para>
/// The operation ids passed to <c>show</c> are taken from the preceding <c>list</c> response, so the
/// fan-out requests are identical in record and playback.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class DeploymentReadScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-deployment-rg";
    private const string DeploymentName = "scenario-deployment";
    private const string NsgName = "scenario-deployment-nsg";
    private const string Location = "westus2";

    private static string TemplateFile =>
        Path.Combine(RepoLocator.GetRepoRoot(), "tests", "Scenario.Tests", "Fixtures", "deployment-nsg.json");

    [Fact]
    public void CreateReadAndEnumerateDeploymentOperations()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(DeploymentReadScenarioTests),
            nameof(CreateReadAndEnumerateDeploymentOperations));

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                useLiveAuth: true,
                extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployment]);

            RunCliWorkflow(host, host.LiveSubscriptionId());
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(
            ClientType.CLI,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployment]))
        {
            RunCliWorkflow(cli, SubscriptionId);
        }

        using (var ps = new ScenarioHost(
            ClientType.PowerShell,
            recording.CreateInterceptor(),
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployment]))
        {
            RunPowerShellWorkflow(ps);
        }
    }

    private static void RunCliWorkflow(ScenarioHost host, string subscriptionId)
    {
        host.RunCli("group", "create", "--subscription-id", subscriptionId, "--name", ResourceGroupName, "--location", Location);

        AssertDeployment(Assert.Single(host.RunCli(
            "deployment", "group", "create",
            "--subscription-id", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", DeploymentName,
            "--template-file", TemplateFile,
            "--parameter", $"location={Location}", $"nsgName={NsgName}")));

        AssertDeployment(Assert.Single(host.RunCli(
            "deployment", "group", "show",
            "--subscription-id", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", DeploymentName)));

        AssertDeploymentListed(host.RunCli(
            "deployment", "group", "list",
            "--subscription-id", subscriptionId,
            "--resource-group", ResourceGroupName));

        AssertDeploymentListed(host.RunCli(
            "deployment", "group", "list",
            "--subscription-id", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--filter", "provisioningState eq 'Succeeded'"));

        List<string> operationIds = AssertOperationsListed(host.RunCli(
            "deployment", "operation", "group", "list",
            "--subscription-id", subscriptionId,
            "--resource-group", ResourceGroupName,
            "--name", DeploymentName));

        AssertOperationsShown(
            host.RunCli(
                "deployment", "operation", "group", "show",
                "--subscription-id", subscriptionId,
                "--resource-group", ResourceGroupName,
                "--name", DeploymentName,
                "--operation-ids", operationIds[0]),
            expectedIds: [operationIds[0]]);

        AssertOperationsShown(
            host.RunCli([
                "deployment", "operation", "group", "show",
                "--subscription-id", subscriptionId,
                "--resource-group", ResourceGroupName,
                "--name", DeploymentName,
                "--operation-ids", .. operationIds]),
            expectedIds: operationIds);

        host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", ResourceGroupName);
    }

    private static void RunPowerShellWorkflow(ScenarioHost host)
    {
        host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = ResourceGroupName,
            ["Location"] = Location,
        });

        AssertDeployment(Assert.Single(host.RunPowerShell("New-AzResourceGroupDeployment", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = DeploymentName,
            ["TemplateFile"] = TemplateFile,
            ["Parameter"] = new[] { $"location={Location}", $"nsgName={NsgName}" },
        })));

        AssertDeployment(Assert.Single(host.RunPowerShell("Get-AzResourceGroupDeployment_Get", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = DeploymentName,
        })));

        AssertDeploymentListed(host.RunPowerShell("Get-AzResourceGroupDeployment_List", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
        }));

        AssertDeploymentListed(host.RunPowerShell("Get-AzResourceGroupDeployment_List", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Filter"] = "provisioningState eq 'Succeeded'",
        }));

        List<string> operationIds = AssertOperationsListed(host.RunPowerShell("Get-AzResourceGroupDeploymentOperation_List", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["DeploymentName"] = DeploymentName,
        }));

        AssertOperationsShown(
            host.RunPowerShell("Get-AzResourceGroupDeploymentOperation_Get", new Dictionary<string, object>
            {
                ["SubscriptionId"] = SubscriptionId,
                ["ResourceGroupName"] = ResourceGroupName,
                ["DeploymentName"] = DeploymentName,
                ["OperationIds"] = new[] { operationIds[0] },
            }),
            expectedIds: [operationIds[0]]);

        AssertOperationsShown(
            host.RunPowerShell("Get-AzResourceGroupDeploymentOperation_Get", new Dictionary<string, object>
            {
                ["SubscriptionId"] = SubscriptionId,
                ["ResourceGroupName"] = ResourceGroupName,
                ["DeploymentName"] = DeploymentName,
                ["OperationIds"] = operationIds.ToArray(),
            }),
            expectedIds: operationIds);

        host.RunPowerShell("Remove-AzResourceGroup", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["Name"] = ResourceGroupName,
        });
    }

    private static void AssertDeployment(object output)
    {
        var deployment = Assert.IsType<DeploymentExtended>(output);
        Assert.Equal(DeploymentName, deployment.Name);
        Assert.Equal("Succeeded", deployment.Properties?.ProvisioningState);
    }

    private static void AssertDeploymentListed(IReadOnlyList<object> outputs)
    {
        var deployments = Assert.IsType<List<DeploymentExtended>>(Assert.Single(outputs));
        Assert.Contains(deployments, deployment => deployment.Name == DeploymentName);
    }

    private static List<string> AssertOperationsListed(IReadOnlyList<object> outputs)
    {
        var operations = Assert.IsType<List<DeploymentOperation>>(Assert.Single(outputs));
        Assert.NotEmpty(operations);
        Assert.All(operations, operation => Assert.NotNull(operation.OperationId));
        return [.. operations.Select(operation => operation.OperationId!)];
    }

    private static void AssertOperationsShown(IReadOnlyList<object> outputs, IReadOnlyList<string> expectedIds)
    {
        var operations = Assert.IsType<List<DeploymentOperation>>(Assert.Single(outputs));
        Assert.Equal(expectedIds, operations.Select(operation => operation.OperationId));
    }
}
