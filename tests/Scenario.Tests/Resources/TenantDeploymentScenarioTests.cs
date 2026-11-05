using System.Collections;
using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class TenantDeploymentScenarioTests
{
    private const string DeploymentName = "scenario-tenant-deployment";
    private const string Location = "eastus";
    private const string DeploymentsPath = "/providers/Microsoft.Resources/deployments/";
    private const string FailedName = "scenario-tenant-failed";
    private const string FailedNoOpsName = "scenario-tenant-failed-noops";
    private const string CorrelationId = "00000000-0000-0000-0000-0000000000c1";

    private static readonly Dictionary<string, object> s_template = new()
    {
        ["$schema"] = "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
        ["contentVersion"] = "1.0.0.0",
        ["resources"] = Array.Empty<object>(),
    };

    // All cases share one recording, TenantDeploymentScenarioTests.json; each case runs on fresh hosts.
    [Fact]
    public void TenantDeploymentScenarios()
    {
        RecordingSession recording = SessionRecordFiles.OpenSession(typeof(TenantDeploymentScenarioTests), nameof(TenantDeploymentScenarioTests));
        ValidateWhatIfCreateShowListExportCancelDeleteTenantDeployment(recording);
        ValidationLevelsWithDerivedName(recording);
        TemplateSpecWithLegacyParameterFile(recording);
        FailedCreateReportsErrorsAndCorrelationId(recording);
        TemplateObjectWithLegacyParameterUri(recording);
        PipedDeploymentObjectsTargetTheirDeployment(recording);
        recording.Save();
    }

    // T2 Tenant deployment lifecycle (scenario "T2 Tenant deployment lifecycle").
    private static void ValidateWhatIfCreateShowListExportCancelDeleteTenantDeployment(RecordingSession recording) =>
        RunScenario(recording, LifecycleCli, LifecyclePowerShell);

    // T1 Validation levels with a derived name (scenario "T1 Validation levels with a derived name").
    private static void ValidationLevelsWithDerivedName(RecordingSession recording) =>
        RunScenario(recording, ValidationLevelsCli, ValidationLevelsPowerShell);

    // T3 Deploy from a template spec with the legacy parameter-file name (scenario of the same title).
    private static void TemplateSpecWithLegacyParameterFile(RecordingSession recording) =>
        RunScenario(recording, cli: null, ps: (host, sent) =>
        {
            const string SpecId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-rg/providers/Microsoft.Resources/templateSpecs/scenario-spec/versions/v1";
            string parameters = Path.GetTempFileName();
            try
            {
                File.WriteAllText(parameters, """{"parameters":{"p1":{"value":"v1"}}}""");
                // The real cmdlet binds the legacy [Alias] name to ParametersFile.
                var deployment = Assert.IsType<DeploymentExtended>(Assert.Single(host.RunPowerShellCmdlet<NewTenantDeploymentCmdlet>(new Dictionary<string, object>
                {
                    ["Name"] = "scenario-tenant-spec",
                    ["Location"] = "westus2",
                    ["TemplateSpecId"] = SpecId,
                    ["TemplateParameterFile"] = parameters,
                })));

                Assert.Equal("Succeeded", deployment.ProvisioningState);
                JsonElement properties = Body(LastSent(sent, "PUT", DeploymentsPath + "scenario-tenant-spec?")).GetProperty("properties");
                Assert.Equal(SpecId, properties.GetProperty("templateLink").GetProperty("id").GetString());
                Assert.Equal("v1", properties.GetProperty("parameters").GetProperty("p1").GetProperty("value").GetString());
            }
            finally
            {
                File.Delete(parameters);
            }
        });

    // T4 A failed create reports errors and the correlation ID (scenario of the same title).
    private static void FailedCreateReportsErrorsAndCorrelationId(RecordingSession recording) =>
        RunScenario(
            recording,
            (host, _) => WithTemplateFile(file => AssertFailedCreates(name => host.RunCli(
                "deployment", "tenant", "create", "--location", Location, "--name", name, "--template-file", file))),
            (host, _) => WithTemplateFile(file => AssertFailedCreates(name => host.RunPowerShell(
                "New-AzTenantDeployment",
                new Dictionary<string, object> { ["Name"] = name, ["Location"] = Location, ["TemplateFile"] = file }))));

    // T5 Template object and legacy parameter URI are sent (scenario of the same title).
    private static void TemplateObjectWithLegacyParameterUri(RecordingSession recording) =>
        RunScenario(recording, cli: null, ps: (host, sent) =>
        {
            host.RunPowerShellCmdlet<NewTenantDeploymentCmdlet>(new Dictionary<string, object>
            {
                ["Name"] = "scenario-tenant-object",
                ["Location"] = Location,
                ["TemplateObject"] = new Hashtable(s_template),
                ["TemplateParameterUri"] = "https://example.com/parameters.json",
            });

            JsonElement properties = Body(LastSent(sent, "PUT", DeploymentsPath + "scenario-tenant-object?")).GetProperty("properties");
            Assert.Equal("1.0.0.0", properties.GetProperty("template").GetProperty("contentVersion").GetString());
            Assert.Equal("https://example.com/parameters.json", properties.GetProperty("parametersLink").GetProperty("uri").GetString());
            Assert.False(properties.TryGetProperty("templateLink", out _));
        });

    // T6 Piped deployment objects target their deployment (scenario of the same title).
    private static void PipedDeploymentObjectsTargetTheirDeployment(RecordingSession recording) =>
        RunScenario(recording, cli: null, ps: (host, sent) =>
        {
            const string Piped = "scenario-tenant-piped";

            Assert.True(Assert.IsType<bool>(Assert.Single(host.RunPowerShellCmdlet<StopTenantDeploymentCmdlet>(new Dictionary<string, object>
            {
                ["InputObject"] = new DeploymentExtended { Id = DeploymentsPath + Piped },
                ["PassThru"] = true,
            }))));
            LastSent(sent, "POST", DeploymentsPath + Piped + "/cancel?");

            Assert.True(Assert.IsType<bool>(Assert.Single(host.RunPowerShellCmdlet<RemoveTenantDeploymentCmdlet>(new Dictionary<string, object>
            {
                ["InputObject"] = new DeploymentExtended { Name = Piped },
                ["PassThru"] = true,
            }))));
            LastSent(sent, "DELETE", DeploymentsPath + Piped + "?");

            int before = sent.Sent.Count;
            var error = Assert.Throws<InvalidOperationException>(() => host.RunPowerShellCmdlet<RemoveTenantDeploymentCmdlet>(
                new Dictionary<string, object> { ["InputObject"] = new DeploymentExtended() }));
            Assert.Contains("The deployment object has neither an Id nor a Name.", error.Message);
            Assert.Equal(before, sent.Sent.Count);
        });

    // Record appends one surface (CLI when it has steps) against a fresh fake backend; playback replays
    // the shared recording on every surface given.
    private static void RunScenario(
        RecordingSession recording,
        Action<ScenarioHost, RecordPlaybackInterceptor>? cli,
        Action<ScenarioHost, RecordPlaybackInterceptor>? ps)
    {
        if (recording.Mode == RecorderMode.Record)
        {
            RecordPlaybackInterceptor interceptor = recording.CreateInterceptor();
            using (var host = new ScenarioHost(
                cli is null ? ClientType.PowerShell : ClientType.CLI,
                interceptor,
                new TenantDeploymentBackend(),
                extensions: ExtensionUnderTest.ResourcesDeployments))
            {
                (cli ?? ps)!(host, interceptor);
            }
            return;
        }

        foreach (var (surface, run) in new[] { (ClientType.CLI, cli), (ClientType.PowerShell, ps) })
        {
            if (run is null)
            {
                continue;
            }
            RecordPlaybackInterceptor interceptor = recording.CreateInterceptor();
            using var host = new ScenarioHost(surface, interceptor, extensions: ExtensionUnderTest.ResourcesDeployments);
            run(host, interceptor);
        }
    }

    private static void LifecycleCli(ScenarioHost host, RecordPlaybackInterceptor sent) => WithTemplateFile(template =>
    {
        Assert.IsType<DeploymentValidateResult>(Assert.Single(host.RunCli(
            "deployment", "tenant", "validate", "--location", Location, "--name", DeploymentName, "--template-file", template)));
        Assert.IsType<WhatIfOperationResult>(Assert.Single(host.RunCli(
            "deployment", "tenant", "what-if", "--location", Location, "--name", DeploymentName, "--template-file", template, "--no-pretty-print")));

        var created = Assert.IsType<DeploymentExtended>(Assert.Single(host.RunCli(
            "deployment", "tenant", "create", "--location", Location, "--name", DeploymentName, "--template-file", template)));
        Assert.Equal("Succeeded", created.ProvisioningState);

        Assert.Contains(Deployments(host.RunCli("deployment", "tenant", "list")), d => d.Name == DeploymentName);
        Assert.Contains(
            Deployments(host.RunCli("deployment", "tenant", "list", "--filter", "provisioningState eq 'Succeeded'")),
            d => d.Name == DeploymentName);
        Assert.Contains("$filter=", Uri.UnescapeDataString(sent.Sent[^1].Uri));

        Assert.Equal(DeploymentName, Assert.IsType<DeploymentExtended>(Assert.Single(
            host.RunCli("deployment", "tenant", "show", "--name", DeploymentName))).Name);
        // Export writes only the template, like legacy CLI.
        var exported = Assert.IsType<JsonElement>(Assert.Single(
            host.RunCli("deployment", "tenant", "export", "--name", DeploymentName)));
        Assert.Equal(JsonValueKind.Array, exported.GetProperty("resources").ValueKind);

        host.RunCli("deployment", "tenant", "cancel", "--name", DeploymentName);
        Assert.Equal("Canceled", Assert.IsType<DeploymentExtended>(Assert.Single(
            host.RunCli("deployment", "tenant", "show", "--name", DeploymentName))).ProvisioningState);

        host.RunCli("deployment", "tenant", "delete", "--name", DeploymentName);
        LastSent(sent, "DELETE", DeploymentsPath + DeploymentName + "?");
    });

    private static void LifecyclePowerShell(ScenarioHost host, RecordPlaybackInterceptor sent)
    {
        DeploymentExtended created = null!;
        WithTemplateFile(template =>
        {
            var body = new Dictionary<string, object>
            {
                ["Name"] = DeploymentName,
                ["Location"] = Location,
                ["TemplateFile"] = template,
            };
            Assert.IsType<DeploymentValidateResult>(Assert.Single(host.RunPowerShell("Test-AzTenantDeployment", body)));
            created = Assert.IsType<DeploymentExtended>(Assert.Single(host.RunPowerShell("New-AzTenantDeployment", body)));
        });
        Assert.Equal("Succeeded", created.ProvisioningState);

        Assert.Equal(created.Name, Assert.IsType<DeploymentExtended>(Assert.Single(host.RunPowerShell(
            "Get-AzTenantDeployment_GetById",
            new Dictionary<string, object> { ["Id"] = DeploymentsPath + DeploymentName }))).Name);
        Assert.Equal(created.Name, Assert.IsType<DeploymentExtended>(Assert.Single(host.RunPowerShell(
            "Get-AzTenantDeployment_Get",
            new Dictionary<string, object> { ["Name"] = DeploymentName }))).Name);

        Assert.NotEmpty(host.RunPowerShell("Get-AzTenantDeployment_List", new Dictionary<string, object>()));
        host.RunPowerShell("Stop-AzTenantDeployment", new Dictionary<string, object> { ["Name"] = DeploymentName });
        host.RunPowerShell("Remove-AzTenantDeployment", new Dictionary<string, object> { ["Name"] = DeploymentName });
        LastSent(sent, "DELETE", DeploymentsPath + DeploymentName + "?");
    }

    private static void ValidationLevelsCli(ScenarioHost host, RecordPlaybackInterceptor sent) => WithDerivedNameTemplate(template =>
    {
        AssertValidated(host.RunCli(
            "deployment", "tenant", "validate", "--location", Location, "--name", "scenario-tenant-validate",
            "--template-file", template, "--validation-level", "Template"), sent);

        var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(host.RunCli(
            "deployment", "tenant", "what-if", "--location", Location,
            "--template-file", template, "--validation-level", "ProviderNoRbac", "--no-pretty-print")));
        Assert.Equal("Succeeded", whatIf.Status);
        Assert.Equal("ProviderNoRbac", ValidationLevel(LastSent(sent, "POST", DeploymentsPath + "empty_template_tenant/whatIf?")));

        AssertDerivedCreate(host.RunCli(
            "deployment", "tenant", "create", "--location", Location,
            "--template-file", template, "--validation-level", "Provider"), sent);
    });

    private static void ValidationLevelsPowerShell(ScenarioHost host, RecordPlaybackInterceptor sent) => WithDerivedNameTemplate(template =>
    {
        AssertValidated(host.RunPowerShell("Test-AzTenantDeployment", new Dictionary<string, object>
        {
            ["Name"] = "scenario-tenant-validate",
            ["Location"] = Location,
            ["TemplateFile"] = template,
            ["ValidationLevel"] = "Template",
        }), sent);

        AssertDerivedCreate(host.RunPowerShell("New-AzTenantDeployment", new Dictionary<string, object>
        {
            ["Location"] = Location,
            ["TemplateFile"] = template,
            ["ValidationLevel"] = "Provider",
        }), sent);
    });

    private static void AssertValidated(IReadOnlyList<object> output, RecordPlaybackInterceptor sent)
    {
        Assert.Equal("Succeeded", Assert.IsType<DeploymentValidateResult>(Assert.Single(output)).ProvisioningState);
        Assert.Equal("Template", ValidationLevel(LastSent(sent, "POST", DeploymentsPath + "scenario-tenant-validate/validate?")));
    }

    private static void AssertDerivedCreate(IReadOnlyList<object> output, RecordPlaybackInterceptor sent)
    {
        Assert.Equal("Succeeded", Assert.IsType<DeploymentExtended>(Assert.Single(output)).ProvisioningState);
        Assert.Equal("Provider", ValidationLevel(LastSent(sent, "PUT", DeploymentsPath + "empty_template_tenant?")));
    }

    private static void AssertFailedCreates(Func<string, IReadOnlyList<object>> create)
    {
        var withOperations = Assert.ThrowsAny<Exception>(() => create(FailedName));
        Assert.Contains($"Deployment '{FailedName}' failed with errors, showing 2 of 2 errors.", withOperations.Message);
        Assert.Contains("ScenarioError1", withOperations.Message);
        Assert.Contains("ScenarioError2", withOperations.Message);
        Assert.Contains($"Correlation ID: {CorrelationId}", withOperations.Message);

        var withoutOperations = Assert.ThrowsAny<Exception>(() => create(FailedNoOpsName));
        Assert.Contains("ScenarioDeploymentError: Scenario deployment failed.", withoutOperations.Message);
        Assert.Contains($"Correlation ID: {CorrelationId}", withoutOperations.Message);
    }

    private static IEnumerable<DeploymentExtended> Deployments(IReadOnlyList<object> output) =>
        output.SelectMany(o => o as IEnumerable<DeploymentExtended> ?? [Assert.IsType<DeploymentExtended>(o)]);

    private static CapturedHttpRequest LastSent(RecordPlaybackInterceptor sent, string method, string uriPart) =>
        Assert.Single(sent.Sent.Where(r => r.Method == method && r.Uri.Contains(uriPart, StringComparison.OrdinalIgnoreCase)).TakeLast(1));

    private static JsonElement Body(CapturedHttpRequest request) => JsonDocument.Parse(request.Body!).RootElement;

    private static string? ValidationLevel(CapturedHttpRequest request) =>
        Body(request).GetProperty("properties").GetProperty("validationLevel").GetString();

    // The CLI only accepts a template from a file/URI/spec, unlike PowerShell's -TemplateObject,
    // so write the shared s_template out to a temp file instead of a committed fixture.
    private static void WithTemplateFile(Action<string> run)
    {
        string template = Path.GetTempFileName();
        try
        {
            File.WriteAllText(template, JsonSerializer.Serialize(s_template));
            run(template);
        }
        finally
        {
            File.Delete(template);
        }
    }

    // A fixed file name, so the deployment name derived from it is the same on every run.
    private static void WithDerivedNameTemplate(Action<string> run)
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string template = Path.Combine(directory, "empty_template_tenant.json");
            File.WriteAllText(template, JsonSerializer.Serialize(s_template));
            run(template);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class TenantDeploymentBackend : HttpMessageHandler
    {
        private bool _canceled;
        private bool _deleted;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            string name = path.StartsWith(DeploymentsPath, StringComparison.OrdinalIgnoreCase)
                ? path[DeploymentsPath.Length..].Split('/')[0]
                : DeploymentName;

            if (request.Method == HttpMethod.Post && path.EndsWith("/validate", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json($$$"""{"name":"{{{name}}}","properties":{"provisioningState":"Succeeded"}}"""));
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
                _canceled = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/operations", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json(name == FailedName ? FailedOperations : """{"value":[]}"""));
            }

            if (request.Method == HttpMethod.Head)
            {
                return Task.FromResult(new HttpResponseMessage(_deleted ? HttpStatusCode.NotFound : HttpStatusCode.NoContent));
            }

            if (request.Method == HttpMethod.Put)
            {
                return Task.FromResult(Json(Deployment(name, false)));
            }

            if (request.Method == HttpMethod.Delete)
            {
                _deleted = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            if (request.Method == HttpMethod.Get &&
                path.Equals("/providers/Microsoft.Resources/deployments", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json($$"""{"value":[{{Deployment(DeploymentName, false)}}]}"""));
            }

            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(_deleted
                    ? Json("""{"error":{"code":"NotFound"}}""", HttpStatusCode.NotFound)
                    : Json(Deployment(name, _canceled)));
            }

            return Task.FromResult(Json("""{"error":{"code":"NotFound"}}""", HttpStatusCode.NotFound));
        }

        private const string FailedOperations =
            """
            {"value":[
              {"operationId":"op1","properties":{"provisioningState":"Failed","statusMessage":{"error":{"code":"ScenarioError1","message":"First resource failed."}}}},
              {"operationId":"op2","properties":{"provisioningState":"Failed","statusMessage":{"error":{"code":"ScenarioError2","message":"Second resource failed."}}}}
            ]}
            """;

        private static string Deployment(string name, bool canceled)
        {
            bool failed = name.StartsWith(FailedName, StringComparison.OrdinalIgnoreCase);
            string state = failed ? "Failed" : canceled ? "Canceled" : "Succeeded";
            string error = name == FailedNoOpsName
                ? ""","error":{"code":"ScenarioDeploymentError","message":"Scenario deployment failed."}"""
                : "";
            return $$"""
            {
              "id": "{{DeploymentsPath}}{{name}}",
              "name": "{{name}}",
              "type": "Microsoft.Resources/deployments",
              "location": "{{Location}}",
              "properties": {
                "provisioningState": "{{state}}",
                "mode": "Incremental",
                "correlationId": "{{(failed ? CorrelationId : "")}}"{{error}}
              }
            }
            """;
        }

        private static HttpResponseMessage Json(
            string body,
            HttpStatusCode statusCode = HttpStatusCode.OK) =>
            new(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
    }
}
