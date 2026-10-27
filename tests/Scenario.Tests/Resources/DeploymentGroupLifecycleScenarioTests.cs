using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Drives <c>deployment group</c> through the real dispatcher, binders, and ARM pipeline in one
/// recorded session and one resource group: validate, what-if, create, show, list, export, the
/// cancel rejections, delete, and finally cancelling an in-flight deployment. Record mode captures
/// the CLI workflow against Azure; playback replays the same traffic through both the CLI and
/// PowerShell surfaces.
/// </summary>
[Collection("Scenario Tests")]
public sealed class DeploymentGroupLifecycleScenarioTests
{
    // A method can't share its class's name, so the recording is named explicitly.
    private const string RecordingName = nameof(DeploymentGroupLifecycleScenarioTests);
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-deployment-lifecycle-rg";
    private const string MissingResourceGroupName = "scenario-deployment-missing-rg";
    // Fixed so the validate URI matches on replay; without --name the command generates a GUID.
    private const string DeploymentName = "scenario-deployment";
    private const string MissingDeploymentName = "scenario-missing-deployment";
    private const string SlowDeploymentName = "scenario-slow-deployment";
    private const string Location = "westus2";
    private const string EmptyTemplate =
        """{"$schema":"https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[]}""";

    // A storage account takes long enough to provision that the deployment is still cancellable.
    // The account name derives from the resource group id, so no run id appears in the request.
    private const string SlowDeploymentBody =
        """{"properties":{"mode":"Incremental","template":{"$schema":"https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#","contentVersion":"1.0.0.0","resources":[{"type":"Microsoft.Storage/storageAccounts","apiVersion":"2023-01-01","name":"[concat('scenario', uniqueString(resourceGroup().id))]","location":"[resourceGroup().location]","sku":{"name":"Standard_LRS"},"kind":"StorageV2"}]}}}""";

    private static readonly ExtensionUnderTest[] s_extensions =
        [ExtensionUnderTest.Resources, ExtensionUnderTest.ResourceGroupDeployments, ExtensionUnderTest.Rest];

    [Fact]
    public void ValidateWhatIfCreateShowListExportCancelDeleteDeployment()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(DeploymentGroupLifecycleScenarioTests), RecordingName);
        string template = Path.GetTempFileName();
        File.WriteAllText(template, EmptyTemplate);

        try
        {
            if (recording.Mode == RecorderMode.Record)
            {
                var groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
                using var host = new ScenarioHost(
                    ClientType.CLI,
                    recording.CreateInterceptor(),
                    useLiveAuth: true,
                    extensions: s_extensions);
                string subscriptionId = host.LiveSubscriptionId();

                try
                {
                    Lifecycle(host, ClientType.CLI, subscriptionId, groupName, template, live: true);
                    recording.Save();
                }
                finally
                {
                    // Runs after the save, so the cleanup never lands in the recording.
                    try { host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName); } catch { }
                }
                return;
            }

            foreach (var surface in new[] { ClientType.CLI, ClientType.PowerShell })
            {
                using var host = new ScenarioHost(surface, recording.CreateInterceptor(), extensions: s_extensions);
                Lifecycle(host, surface, SubscriptionId, ResourceGroupName, template, live: false);
            }
        }
        finally
        {
            File.Delete(template);
        }
    }

    private static void Lifecycle(ScenarioHost host, ClientType surface, string subscriptionId, string groupName, string template, bool live)
    {
        Assert.IsType<ResourceGroup>(Assert.Single(CreateGroup(host, surface, subscriptionId, groupName)));

        var validation = Assert.IsType<DeploymentValidateResult>(
            Assert.Single(Validate(host, surface, subscriptionId, groupName, template)));
        Assert.Null(validation.Error);
        Assert.Equal("Succeeded", validation.Properties?.ProvisioningState);

        var whatIf = Assert.IsType<WhatIfOperationResult>(
            Assert.Single(WhatIf(host, surface, subscriptionId, groupName, template)));
        Assert.Equal("Succeeded", whatIf.Status);
        Assert.Null(whatIf.Error);
        Assert.Empty(whatIf.Properties?.Changes ?? []);

        var created = Assert.IsType<DeploymentExtended>(
            Assert.Single(CreateDeployment(host, surface, subscriptionId, groupName, template)));
        Assert.Equal(DeploymentName, created.Name);
        Assert.Equal("Succeeded", created.Properties?.ProvisioningState);

        var shown = Assert.IsType<DeploymentExtended>(
            Assert.Single(Show(host, surface, subscriptionId, groupName, DeploymentName)));
        Assert.Equal(DeploymentName, shown.Name);
        Assert.Equal("Succeeded", shown.Properties?.ProvisioningState);

        // The list command writes every page as one collected List<DeploymentExtended>.
        var deployments = Assert.IsType<List<DeploymentExtended>>(
            Assert.Single(List(host, surface, subscriptionId, groupName)));
        var listed = Assert.Single(deployments);
        Assert.Equal(DeploymentName, listed.Name);
        Assert.Equal("Succeeded", listed.Properties?.ProvisioningState);

        AssertExportsEmptyTemplate(host, surface, subscriptionId, groupName);

        AssertCancelFails("DeploymentCannotBeCancelled", host, surface, subscriptionId, groupName, DeploymentName);
        AssertCancelFails("DeploymentNotFound", host, surface, subscriptionId, groupName, MissingDeploymentName);
        AssertCancelFails("ResourceGroupNotFound", host, surface, subscriptionId, MissingResourceGroupName, DeploymentName);

        Assert.Empty(Delete(host, surface, subscriptionId, groupName));

        Assert.Empty(Assert.IsType<List<DeploymentExtended>>(Assert.Single(List(host, surface, subscriptionId, groupName))));

        CancelInFlightDeployment(host, surface, subscriptionId, groupName, live);
    }

    // CLI `deployment group export` prints the template; PowerShell
    // `Save-AzResourceGroupDeploymentTemplate` writes it to a file.
    private static void AssertExportsEmptyTemplate(ScenarioHost host, ClientType surface, string subscriptionId, string groupName)
    {
        if (surface == ClientType.CLI)
        {
            AssertEmptyTemplate(Assert.IsType<JsonElement>(Assert.Single(host.RunCli(
                "deployment", "group", "export",
                "--subscription-id", subscriptionId,
                "--resource-group", groupName,
                "--name", DeploymentName))));
            return;
        }

        string path = Path.Combine(Path.GetTempPath(), $"azclips-export-{Guid.NewGuid():N}.json");
        try
        {
            var saved = Assert.IsType<DeploymentTemplateSaveResult>(Assert.Single(host.RunPowerShell(
                "Save-AzResourceGroupDeploymentTemplate",
                new Dictionary<string, object>
                {
                    ["SubscriptionId"] = subscriptionId,
                    ["ResourceGroupName"] = groupName,
                    ["Name"] = DeploymentName,
                    ["Path"] = path,
                    ["Force"] = true,
                })));
            Assert.Equal(path, saved.Path);

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            AssertEmptyTemplate(document.RootElement);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void AssertEmptyTemplate(JsonElement template)
    {
        Assert.True(template.TryGetProperty("$schema", out _), $"Exported template has no $schema: {template}");
        Assert.Equal(0, template.GetProperty("resources").GetArrayLength());
    }

    private static void CancelInFlightDeployment(ScenarioHost host, ClientType surface, string subscriptionId, string groupName, bool live)
    {
        // `deployment group create` blocks until the deployment is terminal, so start the
        // deployment to cancel with a raw PUT that returns as soon as ARM accepts it.
        var response = Assert.IsType<RestResponse>(Assert.Single(StartSlowDeployment(host, surface, subscriptionId, groupName)));
        Assert.Equal(201, response.StatusCode);

        string? state = WaitForState(host, surface, subscriptionId, groupName, live, attempts: 15, "Accepted", "Running");
        Assert.True(state is "Accepted" or "Running", $"The deployment to cancel is '{state}', not cancellable; re-record this test.");

        try
        {
            Assert.Empty(Cancel(host, surface, subscriptionId, groupName, SlowDeploymentName));
        }
        catch (HttpRequestException error) when (error.Message.Contains("DeploymentCannotBeCancelled"))
        {
            Assert.Fail("The deployment finished before it could be cancelled; re-record this test.");
        }

        Assert.Equal("Canceled", WaitForState(host, surface, subscriptionId, groupName, live, attempts: 30, "Canceled"));
    }

    private static void AssertCancelFails(string expectedCode, ScenarioHost host, ClientType surface, string subscriptionId, string groupName, string deploymentName)
    {
        var error = Assert.Throws<HttpRequestException>(() => Cancel(host, surface, subscriptionId, groupName, deploymentName));
        Assert.Contains(expectedCode, error.Message);
    }

    // Stops at a wanted state or any terminal one. Playback replays the recorded responses in
    // order, so it takes the same number of polls; only record mode waits between them.
    private static string? WaitForState(ScenarioHost host, ClientType surface, string subscriptionId, string groupName, bool live, int attempts, params string[] wanted)
    {
        string? state = null;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(Show(host, surface, subscriptionId, groupName, SlowDeploymentName)));
            state = deployment.Properties?.ProvisioningState;
            if (wanted.Contains(state) || state is "Succeeded" or "Failed" or "Canceled")
            {
                break;
            }

            if (live)
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
        return state;
    }

    private static IReadOnlyList<object> CreateGroup(ScenarioHost host, ClientType surface, string subscriptionId, string groupName) =>
        surface == ClientType.CLI
            ? host.RunCli("group", "create", "--subscription-id", subscriptionId, "--name", groupName, "--location", Location)
            : host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = groupName,
                ["Location"] = Location,
            });

    private static IReadOnlyList<object> Validate(ScenarioHost host, ClientType surface, string subscriptionId, string groupName, string template) =>
        surface == ClientType.CLI
            ? host.RunCli(
                "deployment", "group", "validate",
                "--subscription-id", subscriptionId,
                "--resource-group", groupName,
                "--name", DeploymentName,
                "--template-file", template)
            : host.RunPowerShell("Test-AzResourceGroupDeployment", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["ResourceGroupName"] = groupName,
                ["Name"] = DeploymentName,
                ["TemplateFile"] = template,
            });

    private static IReadOnlyList<object> WhatIf(ScenarioHost host, ClientType surface, string subscriptionId, string groupName, string template) =>
        surface == ClientType.CLI
            ? host.RunCli(
                "deployment", "group", "what-if",
                "--subscription-id", subscriptionId,
                "--resource-group", groupName,
                "--name", DeploymentName,
                "--template-file", template)
            : host.RunPowerShell("New-AzResourceGroupDeploymentWhatIf", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["ResourceGroupName"] = groupName,
                ["Name"] = DeploymentName,
                ["TemplateFile"] = template,
            });

    private static IReadOnlyList<object> CreateDeployment(ScenarioHost host, ClientType surface, string subscriptionId, string groupName, string template) =>
        surface == ClientType.CLI
            ? host.RunCli(
                "deployment", "group", "create",
                "--subscription-id", subscriptionId,
                "--resource-group", groupName,
                "--name", DeploymentName,
                "--template-file", template)
            : host.RunPowerShell("New-AzResourceGroupDeployment", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["ResourceGroupName"] = groupName,
                ["Name"] = DeploymentName,
                ["TemplateFile"] = template,
            });

    private static IReadOnlyList<object> Show(ScenarioHost host, ClientType surface, string subscriptionId, string groupName, string deploymentName) =>
        surface == ClientType.CLI
            ? host.RunCli(
                "deployment", "group", "show",
                "--subscription-id", subscriptionId,
                "--resource-group", groupName,
                "--name", deploymentName)
            : host.RunPowerShell("Get-AzResourceGroupDeployment_Get", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["ResourceGroupName"] = groupName,
                ["Name"] = deploymentName,
            });

    private static IReadOnlyList<object> List(ScenarioHost host, ClientType surface, string subscriptionId, string groupName) =>
        surface == ClientType.CLI
            ? host.RunCli(
                "deployment", "group", "list",
                "--subscription-id", subscriptionId,
                "--resource-group", groupName)
            : host.RunPowerShell("Get-AzResourceGroupDeployment_List", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["ResourceGroupName"] = groupName,
            });

    private static IReadOnlyList<object> Delete(ScenarioHost host, ClientType surface, string subscriptionId, string groupName) =>
        surface == ClientType.CLI
            ? host.RunCli(
                "deployment", "group", "delete",
                "--subscription-id", subscriptionId,
                "--resource-group", groupName,
                "--name", DeploymentName)
            : host.RunPowerShell("Remove-AzResourceGroupDeployment", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["ResourceGroupName"] = groupName,
                ["Name"] = DeploymentName,
            });

    private static IReadOnlyList<object> StartSlowDeployment(ScenarioHost host, ClientType surface, string subscriptionId, string groupName)
    {
        string uri = $"https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups/{groupName}/providers/Microsoft.Resources/deployments/{SlowDeploymentName}?api-version=2025-04-01";
        return surface == ClientType.CLI
            ? host.RunCli("rest", "--method", "PUT", "--uri", uri, "--body", SlowDeploymentBody)
            : host.RunPowerShell("Invoke-AzRestMethod", new Dictionary<string, object>
            {
                ["Method"] = "PUT",
                ["Uri"] = uri,
                ["Payload"] = SlowDeploymentBody,
            });
    }

    private static IReadOnlyList<object> Cancel(ScenarioHost host, ClientType surface, string subscriptionId, string groupName, string deploymentName) =>
        surface == ClientType.CLI
            ? host.RunCli(
                "deployment", "group", "cancel",
                "--subscription-id", subscriptionId,
                "--resource-group", groupName,
                "--name", deploymentName)
            : host.RunPowerShell("Stop-AzResourceGroupDeployment", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["ResourceGroupName"] = groupName,
                ["Name"] = deploymentName,
            });
}
