using System.Net;
using System.Text;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;
using Tests.Common.Utilities;
using Tests.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class TenantDeploymentScenarioTests
{
    private const string DeploymentName = "scenario-tenant-deployment";
    private const string Location = "eastus";

    private static readonly Dictionary<string, object> s_template = new()
    {
        ["$schema"] = "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
        ["contentVersion"] = "1.0.0.0",
        ["resources"] = Array.Empty<object>(),
    };

    [Fact]
    public void CliLifecycle()
    {
        RecordingSession recording =
            SessionRecordFiles.OpenSession(typeof(TenantDeploymentScenarioTests), nameof(CliLifecycle));

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(
                ClientType.CLI,
                recording.CreateInterceptor(),
                new TenantDeploymentBackend(),
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
            SessionRecordFiles.OpenSession(typeof(TenantDeploymentScenarioTests), nameof(PowerShellLifecycle));

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(
                ClientType.PowerShell,
                recording.CreateInterceptor(),
                new TenantDeploymentBackend(),
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
        string template = Path.Combine(
            RepoLocator.GetRepoRoot(),
            "tests",
            "Scripts",
            "Bicep",
            "tenant-empty.json");

        Assert.IsType<DeploymentValidateResult>(Assert.Single(host.RunCli(
            "deployment", "tenant", "validate",
            "--location", Location,
            "--name", DeploymentName,
            "--template-file", template)));

        host.RunCli(
            "deployment", "tenant", "what-if",
            "--location", Location,
            "--name", DeploymentName,
            "--template-file", template);

        Assert.IsType<DeploymentExtended>(Assert.Single(host.RunCli(
            "deployment", "tenant", "create",
            "--location", Location,
            "--name", DeploymentName,
            "--template-file", template,
            "--no-wait")));

        Assert.Single(host.RunCli("deployment", "tenant", "wait", "--name", DeploymentName, "--exists"));
        Assert.IsType<DeploymentExtended>(Assert.Single(
            host.RunCli("deployment", "tenant", "show", "--name", DeploymentName)));
        Assert.NotEmpty(host.RunCli("deployment", "tenant", "list"));
        Assert.NotEmpty(host.RunCli("deployment", "tenant", "export", "--name", DeploymentName));
        host.RunCli("deployment", "tenant", "cancel", "--name", DeploymentName);
        host.RunCli("deployment", "tenant", "delete", "--name", DeploymentName, "--no-wait");
        host.RunCli("deployment", "tenant", "wait", "--name", DeploymentName, "--deleted");
    }

    private static void RunPowerShell(ScenarioHost host)
    {
        var body = new Dictionary<string, object>
        {
            ["Name"] = DeploymentName,
            ["Location"] = Location,
            ["TemplateObject"] = s_template,
        };

        Assert.IsType<DeploymentValidateResult>(Assert.Single(
            host.RunPowerShell("Test-AzTenantDeployment", body)));

        Assert.IsType<DeploymentExtended>(Assert.Single(
            host.RunPowerShell("New-AzTenantDeployment", body)));

        Assert.IsType<DeploymentExtended>(Assert.Single(
            host.RunPowerShell(
                "Get-AzTenantDeployment_Get",
                new Dictionary<string, object> { ["Name"] = DeploymentName })));

        Assert.NotEmpty(host.RunPowerShell(
            "Get-AzTenantDeployment_List",
            new Dictionary<string, object>()));

        host.RunPowerShell(
            "Stop-AzTenantDeployment",
            new Dictionary<string, object> { ["Name"] = DeploymentName });

        host.RunPowerShell(
            "Remove-AzTenantDeployment",
            new Dictionary<string, object> { ["Name"] = DeploymentName });
    }

    private sealed class TenantDeploymentBackend : HttpMessageHandler
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
                return Task.FromResult(Json("""{"name":"scenario-tenant-deployment"}"""));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/whatIf", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json("""{"status":"Succeeded","changes":[]}"""));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/exportTemplate", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json("""{"template":{"resources":[]}}"""));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json("{}", HttpStatusCode.OK));
            }

            if (request.Method == HttpMethod.Put)
            {
                return Task.FromResult(Json(Deployment("Accepted")));
            }

            if (request.Method == HttpMethod.Delete)
            {
                _deleted = true;
                return Task.FromResult(Json("{}", HttpStatusCode.OK));
            }

            if (request.Method == HttpMethod.Get &&
                path.Equals("/providers/Microsoft.Resources/deployments", StringComparison.OrdinalIgnoreCase))
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
              "id": "/providers/Microsoft.Resources/deployments/{{DeploymentName}}",
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
