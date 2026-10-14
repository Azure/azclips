using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Records the subscription-scope deployment workflow (<c>deployment sub *</c>) against Azure with
/// a resource-free template, then replays it through both CLI and PowerShell. The deployment is
/// deleted by the workflow itself, so no resource group is needed.
/// </summary>
[Collection("Scenario Tests")]
public sealed class SubscriptionDeploymentScenarioTests : IDisposable
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string DeploymentName = "scenario-sub-deployment";
    private const string Location = "westus2";
    private const string Greeting = "hello";

    private static readonly ExtensionUnderTest s_deploymentSub = ExtensionUnderTest.Resources with
    {
        Verb = "deployment",
        Leaves = ["sub create", "sub wait", "sub show", "sub export", "sub what-if", "sub validate", "sub delete"],
    };

    private readonly string _templateFile;

    public SubscriptionDeploymentScenarioTests()
    {
        _templateFile = Path.Combine(Path.GetTempPath(), $"azclips-sub-deploy-{Guid.NewGuid():N}.json");
        File.WriteAllText(_templateFile, """
            {
              "$schema": "https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "parameters": { "greeting": { "type": "string" } },
              "resources": [],
              "outputs": { "greeting": { "type": "string", "value": "[parameters('greeting')]" } }
            }
            """);
    }

    public void Dispose() => File.Delete(_templateFile);

    [Fact]
    public void CreatesInspectsAndDeletesSubscriptionDeployment()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(SubscriptionDeploymentScenarioTests),
            nameof(CreatesInspectsAndDeletesSubscriptionDeployment));

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: s_deploymentSub);
            RunCliWorkflow(host, host.LiveSubscriptionId());
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_deploymentSub))
        {
            RunCliWorkflow(cli, SubscriptionId);
        }

        using (var powerShell = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_deploymentSub))
        {
            RunPowerShellWorkflow(powerShell, SubscriptionId);
        }
    }

    private void RunCliWorkflow(ScenarioHost host, string subscriptionId)
    {
        string[] Sub(string leaf, params string[] args) =>
            ["deployment", "sub", leaf, "--subscription-id", subscriptionId, "--name", DeploymentName, .. args];
        string[] templateArgs = ["--location", Location, "--template-file", _templateFile, "--parameter", $"greeting={Greeting}"];

        RunWithCleanup(
            () =>
            {
                AssertDeployment(host.RunCli(Sub("create", templateArgs)));
                Assert.Empty(host.RunCli(Sub("wait", "--created", "--interval", "1")));
                AssertDeployment(host.RunCli(Sub("show")));

                var template = Assert.IsType<JsonElement>(Assert.Single(host.RunCli(Sub("export"))));
                Assert.Contains("subscriptionDeploymentTemplate", template.GetProperty("$schema").GetString());

                var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(host.RunCli(Sub("what-if", templateArgs))));
                Assert.Equal("Succeeded", whatIf.Status);

                var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(host.RunCli(Sub("validate", templateArgs))));
                Assert.Null(validation.Error);

                Assert.Empty(host.RunCli(Sub("delete")));
                Assert.Empty(host.RunCli(Sub("wait", "--deleted", "--interval", "1")));
            },
            () => host.RunCli(Sub("delete")));
    }

    private void RunPowerShellWorkflow(ScenarioHost host, string subscriptionId)
    {
        Dictionary<string, object> Target() => new() { ["SubscriptionId"] = subscriptionId, ["Name"] = DeploymentName };
        Dictionary<string, object> WithTemplate() => new(Target())
        {
            ["Location"] = Location,
            ["TemplateFile"] = _templateFile,
            ["Parameter"] = new[] { $"greeting={Greeting}" },
        };

        RunWithCleanup(
            () =>
            {
                AssertDeployment(host.RunPowerShell("New-AzDeployment", WithTemplate()));
                AssertDeployment(host.RunPowerShell("Get-AzDeployment_Get", Target()));

                var validation = Assert.IsType<DeploymentValidateResult>(Assert.Single(host.RunPowerShell("Test-AzDeployment", WithTemplate())));
                Assert.Null(validation.Error);

                Assert.Empty(host.RunPowerShell("Remove-AzDeployment", Target()));
            },
            () => host.RunPowerShell("Remove-AzDeployment", Target()));
    }

    private static void RunWithCleanup(Action workflow, Action cleanup)
    {
        try
        {
            workflow();
        }
        catch (Exception workflowException)
        {
            try
            {
                cleanup();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "The subscription deployment scenario failed and cleanup also failed.",
                    workflowException,
                    cleanupException);
            }

            throw;
        }
    }

    private static void AssertDeployment(IReadOnlyList<object> outputs)
    {
        var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(outputs));
        Assert.Equal(DeploymentName, deployment.Name);
        Assert.Equal("Succeeded", deployment.ProvisioningState);
        Assert.Equal(Greeting, deployment.Properties?.Outputs?["greeting"].Value);
    }
}
