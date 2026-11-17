// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Exercises the deployment and deployment-operation read commands on both client surfaces. A real
/// resource-group deployment is created first (several network security groups, the cheapest resource
/// that still produces deployment operations, so each one yields its own operation), then read back by
/// list and operation by operation. Record mode runs the CLI surface against live Azure and rewrites the
/// recording; playback replays the committed recording without credentials.
/// <para>
/// The operation ids passed to <c>show</c> are taken from the preceding <c>list</c> response, so the
/// fan-out requests are identical in record and playback. <c>show</c> is checked with one id, with the
/// network security group ids in reverse list order (the output must follow the requested order), and
/// with every listed id.
/// </para>
/// <para>
/// The CLI surface outputs the full <see cref="DeploymentOperation"/>; the PowerShell surface outputs the
/// flattened <see cref="PSDeploymentOperation"/>. Request paths carry the subscription, so they are asserted
/// in playback only.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public sealed class DeploymentOperationScenarioTests : IDisposable
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-deployment-rg";
    private const string DeploymentName = "scenario-deployment";
    private const string NsgName = "scenario-deployment-nsg";
    private const int NsgCount = 3;
    private const string NsgResourceType = "Microsoft.Network/networkSecurityGroups";
    private const string NsgPathSegment = "/providers/" + NsgResourceType + "/";
    private const string Location = "westus2";

    // Matches the copy-loop naming in the template the constructor writes.
    private static readonly string[] NsgNames = [.. Enumerable.Range(0, NsgCount).Select(index => $"{NsgName}-{index}")];

    // PowerShell runs in playback only, so its ids carry the placeholder subscription.
    private static readonly string DeploymentId =
        $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Resources/deployments/{DeploymentName}";

    private static readonly string[] NsgIds =
        [.. NsgNames.Select(name => $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}{NsgPathSegment}{name}")];

    private static readonly string[] PSDeploymentOperationProperties =
        ["Id", "OperationId", "ProvisioningState", "StatusCode", "StatusMessage", "TargetResource"];

    private readonly string _templateFile;

    public DeploymentOperationScenarioTests()
    {
        _templateFile = Path.Combine(Path.GetTempPath(), $"azclips-deployment-operations-{Guid.NewGuid():N}.json");
        File.WriteAllText(_templateFile, """
            {
              "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "parameters": {
                "location": {
                  "type": "string"
                },
                "nsgName": {
                  "type": "string"
                },
                "nsgCount": {
                  "type": "int",
                  "minValue": 1
                }
              },
              "resources": [
                {
                  "type": "Microsoft.Network/networkSecurityGroups",
                  "apiVersion": "2023-11-01",
                  "name": "[format('{0}-{1}', parameters('nsgName'), copyIndex())]",
                  "location": "[parameters('location')]",
                  "copy": {
                    "name": "nsgCopy",
                    "count": "[parameters('nsgCount')]"
                  },
                  "properties": {
                    "securityRules": []
                  }
                }
              ],
              "outputs": {
                "nsgIds": {
                  "type": "array",
                  "copy": {
                    "count": "[parameters('nsgCount')]",
                    "input": "[resourceId('Microsoft.Network/networkSecurityGroups', format('{0}-{1}', parameters('nsgName'), copyIndex()))]"
                  }
                }
              }
            }
            """);
    }

    public void Dispose() => File.Delete(_templateFile);

    // T1: verifies "T1 Resource-group operations listed and shown on both surfaces" of resources-deployment-operations-tests.
    [Fact]
    public void CreateDeploymentThenListAndShowOperations()
    {
        // One recording per class: Records/DeploymentOperationScenarioTests/DeploymentOperationScenarioTests.json.
        var recording = SessionRecordFiles.OpenSession(
            typeof(DeploymentOperationScenarioTests),
            nameof(DeploymentOperationScenarioTests));
        var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                useLiveAuth: true,
                extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployments]);

            var subscriptionId = host.LiveSubscriptionId();
            bool workflowSucceeded = false;
            try
            {
                // Request paths would carry the live subscription, so they are not asserted here.
                RunCliWorkflow(host, requests: null, subscriptionId, groupName);
                workflowSucceeded = true;
            }
            finally
            {
                try
                {
                    host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName);
                }
                catch (Exception exception) when (!workflowSucceeded)
                {
                    Console.Error.WriteLine($"Failed to clean up resource group '{groupName}': {exception}");
                }
            }
            recording.Save();
            return;
        }

        RecordPlaybackInterceptor cliRequests = recording.CreateInterceptor();
        using (var cli = new ScenarioHost(
            ClientType.CLI,
            cliRequests,
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployments]))
        {
            RunCliWorkflow(cli, cliRequests, SubscriptionId, groupName);
            cli.RunCli("group", "delete", "--subscription-id", SubscriptionId, "--name", groupName);
        }

        RecordPlaybackInterceptor psRequests = recording.CreateInterceptor();
        using (var ps = new ScenarioHost(
            ClientType.PowerShell,
            psRequests,
            extensions: [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourcesDeployments]))
        {
            RunPowerShellWorkflow(ps, psRequests);
        }
    }

    private void RunCliWorkflow(ScenarioHost host, RecordPlaybackInterceptor? requests, string subscriptionId, string groupName)
    {
        host.RunCli("group", "create", "--subscription-id", subscriptionId, "--name", groupName, "--location", Location);

        AssertDeployment(Assert.Single(host.RunCli(
            "deployment", "group", "create",
            "--subscription-id", subscriptionId,
            "--resource-group", groupName,
            "--name", DeploymentName,
            "--template-file", _templateFile,
            "--parameter", $"location={Location}", $"nsgName={NsgName}", $"nsgCount={NsgCount}")));

        int sent = SentCount(requests);
        List<DeploymentOperation> listed = AssertCliOperationsListed(host.RunCli(
            "deployment", "operation", "group", "list",
            "--subscription-id", subscriptionId,
            "--resource-group", groupName,
            "-n", DeploymentName));
        AssertGetsSent(requests, sent, [OperationsPath(subscriptionId, groupName)]);

        List<string> operationIds = [.. listed.Select(operation => operation.OperationId!)];
        List<string> nsgOperationIds = [.. listed.Where(IsNsgCreate).Select(operation => operation.OperationId!).Reverse()];

        DeploymentOperation first = Assert.Single(AssertShown<DeploymentOperation>(
            host.RunCli(
                "deployment", "operation", "group", "show",
                "--subscription-id", subscriptionId,
                "--resource-group", groupName,
                "-n", DeploymentName,
                "--operation-ids", operationIds[0]),
            [operationIds[0]],
            operation => operation.OperationId));
        Assert.Equal("Succeeded", first.Properties?.ProvisioningState);
        Assert.Equal(Listed(listed, first.OperationId).Properties?.ProvisioningOperation, first.Properties?.ProvisioningOperation);

        sent = SentCount(requests);
        List<DeploymentOperation> nsgOperations = AssertShown<DeploymentOperation>(
            host.RunCli([
                "deployment", "operation", "group", "show",
                "--subscription-id", subscriptionId,
                "--resource-group", groupName,
                "--name", DeploymentName,
                "--operation-ids", .. nsgOperationIds]),
            nsgOperationIds,
            operation => operation.OperationId);
        AssertGetsSent(requests, sent, [.. nsgOperationIds.Select(id => OperationPath(subscriptionId, id, groupName))]);
        Assert.All(nsgOperations, operation =>
        {
            Assert.Equal(ProvisioningOperation.Create, operation.Properties?.ProvisioningOperation);
            Assert.Equal(NsgResourceType, operation.Properties?.TargetResource?.ResourceType, ignoreCase: true);
            Assert.Equal("Succeeded", operation.Properties?.ProvisioningState);
        });

        List<DeploymentOperation> allOperations = AssertShown<DeploymentOperation>(
            host.RunCli([
                "deployment", "operation", "group", "show",
                "--subscription-id", subscriptionId,
                "--resource-group", groupName,
                "--name", DeploymentName,
                "--operation-ids", .. operationIds]),
            operationIds,
            operation => operation.OperationId);
        Assert.All(allOperations, operation =>
        {
            DeploymentOperation match = Listed(listed, operation.OperationId);
            Assert.Equal(match.Properties?.ProvisioningOperation, operation.Properties?.ProvisioningOperation);
            Assert.Equal(match.Properties?.ProvisioningState, operation.Properties?.ProvisioningState);
            Assert.Equal(match.Properties?.StatusCode, operation.Properties?.StatusCode);
            Assert.Equal(match.Properties?.TargetResource?.Id, operation.Properties?.TargetResource?.Id);
        });

    }

    private void RunPowerShellWorkflow(ScenarioHost host, RecordPlaybackInterceptor requests)
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
            ["TemplateFile"] = _templateFile,
            ["Parameter"] = new[] { $"location={Location}", $"nsgName={NsgName}", $"nsgCount={NsgCount}" },
        })));

        int sent = SentCount(requests);
        List<PSDeploymentOperation> listed = AssertPowerShellOperationsListed(host.RunPowerShell("Get-AzResourceGroupDeploymentOperation_List", new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["DeploymentName"] = DeploymentName,
        }));
        AssertGetsSent(requests, sent, [OperationsPath(SubscriptionId)]);

        List<string> operationIds = [.. listed.Select(operation => operation.OperationId!)];
        List<string> nsgOperationIds =
        [
            .. listed
                .Where(operation => operation.TargetResource?.Contains(NsgPathSegment, StringComparison.OrdinalIgnoreCase) == true)
                .Select(operation => operation.OperationId!)
                .Reverse(),
        ];

        PSDeploymentOperation first = Assert.Single(AssertShown<PSDeploymentOperation>(
            host.RunPowerShell("Get-AzResourceGroupDeploymentOperation_Get", new Dictionary<string, object>
            {
                ["SubscriptionId"] = SubscriptionId,
                ["ResourceGroupName"] = ResourceGroupName,
                ["DeploymentName"] = DeploymentName,
                ["OperationIds"] = new[] { operationIds[0] },
            }),
            [operationIds[0]],
            operation => operation.OperationId));
        AssertSameAsListed(first, listed);

        sent = SentCount(requests);
        List<PSDeploymentOperation> nsgOperations = AssertShown<PSDeploymentOperation>(
            host.RunPowerShell("Get-AzResourceGroupDeploymentOperation_Get", new Dictionary<string, object>
            {
                ["SubscriptionId"] = SubscriptionId,
                ["ResourceGroupName"] = ResourceGroupName,
                ["DeploymentName"] = DeploymentName,
                ["OperationIds"] = nsgOperationIds.ToArray(),
            }),
            nsgOperationIds,
            operation => operation.OperationId);
        AssertGetsSent(requests, sent, [.. nsgOperationIds.Select(id => OperationPath(SubscriptionId, id))]);
        Assert.All(nsgOperations, operation => Assert.Equal(Listed(listed, operation.OperationId).TargetResource, operation.TargetResource));

        List<PSDeploymentOperation> allOperations = AssertShown<PSDeploymentOperation>(
            host.RunPowerShell("Get-AzResourceGroupDeploymentOperation_Get", new Dictionary<string, object>
            {
                ["SubscriptionId"] = SubscriptionId,
                ["ResourceGroupName"] = ResourceGroupName,
                ["DeploymentName"] = DeploymentName,
                ["OperationIds"] = operationIds.ToArray(),
            }),
            operationIds,
            operation => operation.OperationId);
        Assert.All(allOperations, operation => AssertSameAsListed(operation, listed));

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

    /// <summary>
    /// Asserts the CLI list carries the full operation model: one create operation per network security group
    /// and one output evaluation without a target resource, all succeeded.
    /// </summary>
    private static List<DeploymentOperation> AssertCliOperationsListed(IReadOnlyList<object> outputs)
    {
        var operations = Assert.IsType<List<DeploymentOperation>>(Assert.Single(outputs));
        Assert.All(operations, operation =>
        {
            Assert.NotNull(operation.OperationId);
            Assert.Equal("Succeeded", operation.Properties?.ProvisioningState);
        });

        Assert.Equal(
            NsgNames.Order(StringComparer.Ordinal),
            operations.Where(IsNsgCreate).Select(operation => operation.Properties!.TargetResource!.ResourceName).Order(StringComparer.Ordinal));

        DeploymentOperation outputEvaluation = Assert.Single(operations, operation => !IsNsgCreate(operation));
        Assert.Equal(ProvisioningOperation.EvaluateDeploymentOutput, outputEvaluation.Properties?.ProvisioningOperation);
        Assert.Null(outputEvaluation.Properties?.TargetResource);

        return operations;
    }

    private static bool IsNsgCreate(DeploymentOperation operation) =>
        operation.Properties is { ProvisioningOperation: ProvisioningOperation.Create, TargetResource.ResourceType: { } resourceType }
        && string.Equals(resourceType, NsgResourceType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Asserts the PowerShell list carries only the flattened operation: its six properties, the operation and
    /// target resource ids, and no status message, since every recorded operation succeeded.
    /// </summary>
    private static List<PSDeploymentOperation> AssertPowerShellOperationsListed(IReadOnlyList<object> outputs)
    {
        var operations = Assert.IsType<List<PSDeploymentOperation>>(Assert.Single(outputs));
        Assert.Equal(
            PSDeploymentOperationProperties.Order(StringComparer.Ordinal),
            typeof(PSDeploymentOperation).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).Order(StringComparer.Ordinal));

        Assert.All(operations, operation =>
        {
            Assert.NotNull(operation.OperationId);
            Assert.Equal("Succeeded", operation.ProvisioningState);
            Assert.Equal("OK", operation.StatusCode);
            Assert.Null(operation.StatusMessage);
            Assert.Equal($"{DeploymentId}/operations/{operation.OperationId}", operation.Id, ignoreCase: true);
        });

        Assert.Equal(
            NsgIds.Order(StringComparer.OrdinalIgnoreCase),
            operations.Where(operation => operation.TargetResource is not null).Select(operation => operation.TargetResource!).Order(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        Assert.Single(operations, operation => operation.TargetResource is null);

        return operations;
    }

    /// <summary>Asserts one list was written, holding the requested operations in the requested order.</summary>
    private static List<T> AssertShown<T>(IReadOnlyList<object> outputs, IReadOnlyList<string> expectedIds, Func<T, string?> operationId)
    {
        var operations = Assert.IsType<List<T>>(Assert.Single(outputs));
        Assert.Equal(expectedIds, operations.Select(operationId));
        return operations;
    }

    private static DeploymentOperation Listed(List<DeploymentOperation> listed, string? operationId) =>
        Assert.Single(listed, operation => operation.OperationId == operationId);

    private static PSDeploymentOperation Listed(List<PSDeploymentOperation> listed, string? operationId) =>
        Assert.Single(listed, operation => operation.OperationId == operationId);

    private static void AssertSameAsListed(PSDeploymentOperation operation, List<PSDeploymentOperation> listed)
    {
        PSDeploymentOperation match = Listed(listed, operation.OperationId);
        Assert.Equal(match.Id, operation.Id);
        Assert.Equal(match.OperationId, operation.OperationId);
        Assert.Equal(match.ProvisioningState, operation.ProvisioningState);
        Assert.Equal(match.StatusCode, operation.StatusCode);
        Assert.Equal(match.StatusMessage, operation.StatusMessage);
        Assert.Equal(match.TargetResource, operation.TargetResource);
    }

    private static int SentCount(RecordPlaybackInterceptor? requests) => requests?.Sent.Count ?? 0;

    /// <summary>
    /// Asserts that exactly the GETs to <paramref name="expectedPaths"/> were sent since <paramref name="from"/>, in
    /// that order. Record mode passes no interceptor, because its paths carry the live subscription.
    /// </summary>
    private static void AssertGetsSent(RecordPlaybackInterceptor? requests, int from, IReadOnlyList<string> expectedPaths)
    {
        if (requests is null)
        {
            return;
        }

        List<CapturedHttpRequest> sent = [.. requests.Sent.Skip(from)];
        Assert.All(sent, request => Assert.Equal("GET", request.Method));
        Assert.Equal(expectedPaths, sent.Select(request => new Uri(request.Uri).AbsolutePath));
    }

    // The resource-group route has no providers/Microsoft.Resources segment.
    private static string OperationsPath(string subscriptionId, string groupName = ResourceGroupName) =>
        $"/subscriptions/{subscriptionId}/resourcegroups/{groupName}/deployments/{DeploymentName}/operations";

    private static string OperationPath(string subscriptionId, string operationId, string groupName = ResourceGroupName) =>
        $"{OperationsPath(subscriptionId, groupName)}/{operationId}";
}
