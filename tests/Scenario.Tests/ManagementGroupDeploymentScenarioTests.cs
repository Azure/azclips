using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class ManagementGroupDeploymentScenarioTests
{
    private const string ManagementGroupId = "scenario-management-group";
    private const string DeploymentName = "scenario-management-group-deployment";
    private const string Location = "eastus";

    private static readonly Dictionary<string, object> s_template = new()
    {
        ["$schema"] = "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
        ["contentVersion"] = "1.0.0.0",
        ["resources"] = Array.Empty<object>(),
    };

    [Fact]
    public void CliLifecycle()
    {
        RecordingSession recording =
            SessionRecordFiles.OpenSession(typeof(ManagementGroupDeploymentScenarioTests), nameof(CliLifecycle));

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                new ManagementGroupDeploymentBackend(),
                extensions: ExtensionUnderTest.ResourcesDeployments);
            RunCli(host);
            recording.Save();
            return;
        }

        using var playback = new ScenarioHost(
            ClientType.CLI,
            recording.CreateInterceptor(),
            extensions: ExtensionUnderTest.ResourcesDeployments);
        RunCli(playback);
    }

    [Fact]
    public void PowerShellLifecycle()
    {
        RecordingSession recording =
            SessionRecordFiles.OpenSession(typeof(ManagementGroupDeploymentScenarioTests), nameof(PowerShellLifecycle));

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(
                ClientType.PowerShell,
                recording.CreateInterceptor(),
                new ManagementGroupDeploymentBackend(),
                extensions: ExtensionUnderTest.ResourcesDeployments);
            RunPowerShell(host);
            recording.Save();
            return;
        }

        using var playback = new ScenarioHost(
            ClientType.PowerShell,
            recording.CreateInterceptor(),
            extensions: ExtensionUnderTest.ResourcesDeployments);
        RunPowerShell(playback);
    }

    private static void RunCli(ScenarioHost host)
    {
        // The CLI only accepts a template from a file/URI/spec, unlike PowerShell's
        // -TemplateObject, so write the shared s_template out to a temp file instead of
        // depending on a committed Bicep/JSON fixture.
        string template = Path.GetTempFileName();
        try
        {
            File.WriteAllText(template, JsonSerializer.Serialize(s_template));

            Assert.IsType<JsonElement>(Assert.Single(host.RunCli(
                "deployment", "mg", "validate",
                "--location", Location,
                "--management-group-id", ManagementGroupId, "--name", DeploymentName,
                "--template-file", template)));

            Assert.Contains("Resource changes: no change.", Assert.IsType<string>(Assert.Single(host.RunCli(
                "deployment", "mg", "what-if",
                "--location", Location,
                "--management-group-id", ManagementGroupId, "--name", DeploymentName,
                "--template-file", template))));

            Assert.IsType<JsonElement>(Assert.Single(host.RunCli(
                "deployment", "mg", "create",
                "--location", Location,
                "--management-group-id", ManagementGroupId, "--name", DeploymentName,
                "--template-file", template)));

            Assert.Empty(host.RunCli("deployment", "mg", "wait", "--management-group-id", ManagementGroupId, "--name", DeploymentName, "--exists"));
            Assert.IsType<DeploymentExtended>(Assert.Single(
                host.RunCli("deployment", "mg", "show", "--management-group-id", ManagementGroupId, "--name", DeploymentName)));
            Assert.NotEmpty(host.RunCli("deployment", "mg", "list", "--management-group-id", ManagementGroupId));
            Assert.NotEmpty(host.RunCli("deployment", "mg", "export", "--management-group-id", ManagementGroupId, "--name", DeploymentName));
            host.RunCli("deployment", "mg", "cancel", "--management-group-id", ManagementGroupId, "--name", DeploymentName);
            host.RunCli("deployment", "mg", "delete", "--management-group-id", ManagementGroupId, "--name", DeploymentName);
            host.RunCli("deployment", "mg", "wait", "--management-group-id", ManagementGroupId, "--name", DeploymentName, "--deleted");
        }
        finally
        {
            File.Delete(template);
        }
    }

    private static void RunPowerShell(ScenarioHost host)
    {
        var body = new Dictionary<string, object>
        {
            ["ManagementGroupId"] = ManagementGroupId,
            ["Name"] = DeploymentName,
            ["Location"] = Location,
            ["TemplateObject"] = s_template,
        };

        Assert.Empty(host.RunPowerShell("Test-AzManagementGroupDeployment", body));

        var createBody = new Dictionary<string, object>(body)
        {
            ["Tag"] = new Dictionary<string, string> { ["scenario"] = "management-group" },
        };
        Assert.IsType<PSDeployment>(Assert.Single(
            host.RunPowerShell("New-AzManagementGroupDeployment", createBody)));

        Assert.IsType<PSDeployment>(Assert.Single(
            host.RunPowerShell(
                "Get-AzManagementGroupDeployment_Get",
                new Dictionary<string, object> { ["ManagementGroupId"] = ManagementGroupId, ["Name"] = DeploymentName })));

        Assert.NotEmpty(host.RunPowerShell(
            "Get-AzManagementGroupDeployment_List",
            new Dictionary<string, object> { ["ManagementGroupId"] = ManagementGroupId }));

        host.RunPowerShell(
            "Stop-AzManagementGroupDeployment",
            new Dictionary<string, object> { ["ManagementGroupId"] = ManagementGroupId, ["Name"] = DeploymentName });

        host.RunPowerShell(
            "Remove-AzManagementGroupDeployment",
            new Dictionary<string, object> { ["ManagementGroupId"] = ManagementGroupId, ["Name"] = DeploymentName });
    }

    private sealed class ManagementGroupDeploymentBackend : HttpMessageHandler
    {
        private int _deploymentGetCount;
        private bool _deleted;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Post && path.EndsWith("/validate", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json("""{"name":"scenario-management-group-deployment"}"""));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/whatIf", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json("""{"status":"Succeeded","properties":{"changes":[]}}"""));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/exportTemplate", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json("""{"template":{"resources":[]}}"""));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/operations", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json("""{"value":[]}"""));
            }

            if (request.Method == HttpMethod.Head)
            {
                return Task.FromResult(new HttpResponseMessage(_deleted ? HttpStatusCode.NotFound : HttpStatusCode.NoContent));
            }

            if (request.Method == HttpMethod.Put)
            {
                return Task.FromResult(Json(Deployment("Succeeded")));
            }

            if (request.Method == HttpMethod.Delete)
            {
                _deleted = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            if (request.Method == HttpMethod.Get &&
                path.Equals($"/providers/Microsoft.Management/managementGroups/{ManagementGroupId}/providers/Microsoft.Resources/deployments", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json($$"""{"value":[{{Deployment("Succeeded")}}]}"""));
            }

            if (request.Method == HttpMethod.Get)
            {
                if (_deleted)
                {
                    return Task.FromResult(Json(
                        """{"error":{"code":"NotFound"}}""",
                        HttpStatusCode.NotFound));
                }

                _deploymentGetCount++;
                string state = _deploymentGetCount >= 3 ? "Running" : "Succeeded";
                return Task.FromResult(Json(Deployment(state)));
            }

            return Task.FromResult(Json("""{"error":{"code":"NotFound"}}""", HttpStatusCode.NotFound));
        }

        private static string Deployment(string state) =>
            $$"""
            {
              "id": "/providers/Microsoft.Management/managementGroups/{{ManagementGroupId}}/providers/Microsoft.Resources/deployments/{{DeploymentName}}",
              "name": "{{DeploymentName}}",
              "type": "Microsoft.Resources/deployments",
              "location": "{{Location}}",
              "properties": {
                "provisioningState": "{{state}}",
                "mode": "Incremental"
              }
            }
            """;

        private static HttpResponseMessage Json(
            string body,
            HttpStatusCode statusCode = HttpStatusCode.OK) =>
            new(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
    }
}
