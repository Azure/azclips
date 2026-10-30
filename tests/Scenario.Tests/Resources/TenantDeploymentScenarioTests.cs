using System.Collections;
using System.Management.Automation;
using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
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
    private const string EndToEndName = "scenario-tenant-e2e";
    // Playback matches the first unconsumed entry in the whole recording, so the PowerShell run
    // needs its own name to replay its own tagged PUT response rather than the CLI one.
    private const string EndToEndPowerShellName = "scenario-tenant-e2e-ps";
    private const string WhatIfName = "scenario-tenant-whatif";
    private const string PreviewName = "scenario-tenant-preview";
    private const string ParameterTemplateName = "tenant_level_template";
    private const string BicepTemplateName = "tenant_level_bicep";
    private const string ManagementGroup = "scenario-mg";
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroup = "scenario-rg";
    private const string RemoteTemplateUri = "https://example.com/templates/remote_template.json";
    private const string TemplateSpecVersionId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-rg/providers/Microsoft.Resources/templateSpecs/scenario-spec/versions/v1.0";

    private static readonly Dictionary<string, string> s_tags = new() { ["key1"] = "value1", ["key2"] = "value2" };

    private static readonly Dictionary<string, object> s_parameterTemplate = new()
    {
        ["$schema"] = "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
        ["contentVersion"] = "1.0.0.0",
        ["parameters"] = new Dictionary<string, object>
        {
            ["targetMG"] = new Dictionary<string, object> { ["type"] = "string" },
            ["nestedSubId"] = new Dictionary<string, object> { ["type"] = "string" },
            ["nestedRG"] = new Dictionary<string, object> { ["type"] = "string", ["defaultValue"] = ResourceGroup },
        },
        ["resources"] = Array.Empty<object>(),
    };

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
        EndToEndWithParametersTagsSaveAndOperations(recording);
        TenantWhatIfResults(recording);
        PreviewCreateWithWhatIf(recording);
        // T10 compiles its Bicep file with the local Bicep CLI even in playback, so it needs Bicep to record
        // and is left out of playback where Bicep is not installed (CI); its recorded entries then go unused.
        if (recording.Mode == RecorderMode.Record || IsBicepInstalled())
        {
            DeployFromBicepFile(recording);
        }
        recording.Save();
    }

    // T11 Default deployment names, like azure-cli: template file, URI, then Template Spec version name, else 'deployment1'.
    [Fact]
    public void DefaultDeploymentNamesCli() => WithDerivedNameTemplate(template =>
    {
        Assert.Equal("empty_template_tenant", ValidatedName(ClientType.CLI, host => host.RunCli(
            "deployment", "tenant", "validate", "--location", Location, "--template-file", template)));
        Assert.Equal("remote_template", ValidatedName(ClientType.CLI, host => host.RunCli(
            "deployment", "tenant", "validate", "--location", Location, "--template-uri", RemoteTemplateUri)));
        Assert.Equal("v1", ValidatedName(ClientType.CLI, host => host.RunCli(
            "deployment", "tenant", "validate", "--location", Location, "--template-spec-id", TemplateSpecVersionId)));

        // With no template file, URI or spec the template comes from the .bicepparam, compiled by the Bicep CLI.
        if (IsBicepInstalled())
        {
            string directory = Directory.CreateTempSubdirectory().FullName;
            try
            {
                File.WriteAllText(Path.Combine(directory, "main.bicep"), "targetScope = 'tenant'\n\noutput scope string = 'tenant'\n");
                string parameters = Path.Combine(directory, "main.bicepparam");
                File.WriteAllText(parameters, "using 'main.bicep'\n");
                Assert.Equal("deployment1", ValidatedName(ClientType.CLI, host => host.RunCli(
                    "deployment", "tenant", "validate", "--location", Location, "--parameters-file", parameters)));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    });

    // T11 Default deployment names, like legacy PowerShell: template file or URI name, else a GUID.
    [Fact]
    public void DefaultDeploymentNamesPowerShell() => WithDerivedNameTemplate(template =>
    {
        Assert.Equal("empty_template_tenant", ValidatedName(ClientType.PowerShell, host => host.RunPowerShellCmdlet<TestTenantDeploymentCmdlet>(
            new Dictionary<string, object> { ["Location"] = Location, ["TemplateFile"] = template })));
        Assert.Equal("remote_template", ValidatedName(ClientType.PowerShell, host => host.RunPowerShellCmdlet<TestTenantDeploymentCmdlet>(
            new Dictionary<string, object> { ["Location"] = Location, ["TemplateUri"] = RemoteTemplateUri })));
        Assert.True(Guid.TryParse(ValidatedName(ClientType.PowerShell, host => host.RunPowerShellCmdlet<TestTenantDeploymentCmdlet>(
            new Dictionary<string, object> { ["Location"] = Location, ["TemplateSpecId"] = TemplateSpecVersionId })), out _));
        Assert.True(Guid.TryParse(ValidatedName(ClientType.PowerShell, host => host.RunPowerShellCmdlet<TestTenantDeploymentCmdlet>(
            new Dictionary<string, object> { ["Location"] = Location, ["TemplateObject"] = new Hashtable(s_template) })), out _));
    });

    // Like legacy, a failed validate is written as PSResourceManagerError objects (an empty Target becomes null) instead of throwing.
    [Fact]
    public void ValidateFailureWritesPSResourceManagerError()
    {
        var http = new CapturingTracingInterceptor(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"error":{"code":"InvalidTemplate","message":"Template is invalid.","target":"","details":[{"code":"Inner","message":"Bad value.","target":"tier"}]}}""",
                Encoding.UTF8, "application/json"),
        });
        IReadOnlyList<object> output;
        using (var host = new ScenarioHost(ClientType.PowerShell, http, extensions: ExtensionUnderTest.ResourcesDeployments))
        {
            output = host.RunPowerShellCmdlet<TestTenantDeploymentCmdlet>(
                new Dictionary<string, object> { ["Location"] = Location, ["TemplateObject"] = new Hashtable(s_template) });
        }
        var errors = Assert.IsType<List<PSResourceManagerError>>(Assert.Single(output));
        var error = Assert.Single(errors);
        Assert.Equal("InvalidTemplate", error.Code);
        Assert.Equal("Template is invalid.", error.Message);
        Assert.Null(error.Target);
        var detail = Assert.Single(error.Details!);
        Assert.Equal(("Inner", "Bad value.", "tier"), (detail.Code, detail.Message, detail.Target));
    }

    // Like legacy, with only a .bicepparam file the template its 'using' names supplies the dynamic parameters,
    // and their values are compiled into the .bicepparam as overrides. Needs the Bicep CLI.
    [Fact]
    public void BicepParamTemplateParametersBindAsDynamicParameters()
    {
        if (!IsBicepInstalled())
        {
            return;
        }
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "main.bicep"),
                "targetScope = 'tenant'\n\nparam storageName string\nparam tier string = 'Standard'\n\noutput result string = '${storageName}-${tier}'\n");
            string parameters = Path.Combine(directory, "main.bicepparam");
            File.WriteAllText(parameters, "using 'main.bicep'\n\nparam storageName = 'fromfile'\n");
            var http = new CapturingTracingInterceptor(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"properties":{"provisioningState":"Succeeded"}}""", Encoding.UTF8, "application/json"),
            });
            using (var host = new ScenarioHost(ClientType.PowerShell, http, extensions: ExtensionUnderTest.ResourcesDeployments))
            {
                host.RunPowerShellCmdlet<TestTenantDeploymentCmdlet>(new Dictionary<string, object>
                {
                    ["Location"] = Location,
                    ["ParametersFile"] = parameters,
                    ["storageName"] = "fromdynamic",
                    ["tier"] = "Premium",
                });
            }
            CapturedHttpRequest validate = Assert.Single(http.Captured, r => r.Method == "POST");
            Assert.Equal("fromdynamic", TemplateParameter(validate, "storageName"));
            Assert.Equal("Premium", TemplateParameter(validate, "tier"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Like legacy TemplateUtility.ResolveParameterType, a dynamic parameter follows its '$ref' chain to the declared
    // type, so a string bound to a referenced int is sent as a number.
    [Fact]
    public void ReferencedTypeDynamicParameterIsTyped()
    {
        var http = new CapturingTracingInterceptor(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"properties":{"provisioningState":"Succeeded"}}""", Encoding.UTF8, "application/json"),
        });
        var template = new Hashtable(s_template)
        {
            ["definitions"] = new Hashtable
            {
                ["count"] = new Hashtable { ["$ref"] = "#/definitions/baseInt" },
                ["baseInt"] = new Hashtable { ["type"] = "int" },
            },
            ["parameters"] = new Hashtable { ["instanceCount"] = new Hashtable { ["$ref"] = "#/definitions/count" } },
        };
        using (var host = new ScenarioHost(ClientType.PowerShell, http, extensions: ExtensionUnderTest.ResourcesDeployments))
        {
            host.RunPowerShellCmdlet<TestTenantDeploymentCmdlet>(new Dictionary<string, object>
            {
                ["Location"] = Location,
                ["TemplateObject"] = template,
                ["instanceCount"] = "2",
            });
        }
        JsonElement value = Body(Assert.Single(http.Captured, r => r.Method == "POST"))
            .GetProperty("properties").GetProperty("parameters").GetProperty("instanceCount").GetProperty("value");
        Assert.Equal(JsonValueKind.Number, value.ValueKind);
        Assert.Equal(2, value.GetInt32());
    }

    // A GUID name cannot be replayed from a recording, so like DeploymentGroupLifecycleScenarioTests this answers
    // validate with a canned response and returns the deployment name from the request URI.
    private static string ValidatedName(ClientType surface, Action<ScenarioHost> run)
    {
        var http = new CapturingTracingInterceptor(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"properties":{"provisioningState":"Succeeded"}}""", Encoding.UTF8, "application/json"),
        });
        using (var host = new ScenarioHost(surface, http, extensions: ExtensionUnderTest.ResourcesDeployments))
        {
            run(host);
        }
        string path = new Uri(Assert.Single(http.Captured, r => r.Method == "POST").Uri).AbsolutePath;
        Assert.StartsWith(DeploymentsPath, path);
        Assert.EndsWith("/validate", path);
        return path[DeploymentsPath.Length..^"/validate".Length];
    }

    // T10 Deploy from a Bicep file (scenario of the same title).
    private static void DeployFromBicepFile(RecordingSession recording) => RunScenario(recording, BicepFileCli, ps: null);

    private static void BicepFileCli(ScenarioHost host, RecordPlaybackInterceptor sent)
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string template = Path.Combine(directory, BicepTemplateName + ".bicep");
            File.WriteAllText(template, "targetScope = 'tenant'\n\noutput scope string = 'tenant'\n");

            var validated = Assert.IsType<DeploymentValidateResult>(Assert.Single(host.RunCli(
                "deployment", "tenant", "validate", "--location", "WestUS", "--template-file", template)));
            Assert.Equal("Succeeded", validated.Properties?.ProvisioningState);
            AssertCompiledBicep(LastSent(sent, "POST", DeploymentsPath + BicepTemplateName + "/validate?"));

            var whatIf = Assert.IsType<WhatIfOperationResult>(Assert.Single(host.RunCli(
                "deployment", "tenant", "what-if", "--location", "WestUS", "--template-file", template, "--no-pretty-print")));
            Assert.Equal("Succeeded", whatIf.Status);
            AssertCompiledBicep(LastSent(sent, "POST", DeploymentsPath + BicepTemplateName + "/whatIf?"));

            var created = Assert.IsType<DeploymentExtended>(Assert.Single(host.RunCli(
                "deployment", "tenant", "create", "--location", "WestUS", "--template-file", template)));
            Assert.Equal("Succeeded", created.ProvisioningState);
            AssertCompiledBicep(LastSent(sent, "PUT", DeploymentsPath + BicepTemplateName + "?"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // The request carries the ARM JSON compiled from the Bicep source, not the source itself.
    private static void AssertCompiledBicep(CapturedHttpRequest request)
    {
        JsonElement template = Body(request).GetProperty("properties").GetProperty("template");
        Assert.Contains("tenantDeploymentTemplate.json", template.GetProperty("$schema").GetString());
        Assert.Equal("tenant", template.GetProperty("outputs").GetProperty("scope").GetProperty("value").GetString());
    }

    // Mirrors BicepCompiler's lookup: PATH first, then the az-bundled copy.
    private static bool IsBicepInstalled()
    {
        string name = OperatingSystem.IsWindows() ? "bicep.exe" : "bicep";
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Append(Path.Combine(home, ".azure", "bin"))
            .Any(dir => File.Exists(Path.Combine(dir.Trim(), name)));
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
                var deployment = Assert.IsType<PSDeployment>(Assert.Single(host.RunPowerShellCmdlet<NewTenantDeploymentCmdlet>(new Dictionary<string, object>
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
                ["InputObject"] = new PSDeployment { Id = DeploymentsPath + Piped },
                ["PassThru"] = true,
            }))));
            LastSent(sent, "POST", DeploymentsPath + Piped + "/cancel?");

            // -AsJob returns the job at once; the job runs the delete and then outputs the -PassThru value.
            var job = Assert.IsAssignableFrom<Job>(Assert.Single(host.RunPowerShellCmdlet<RemoveTenantDeploymentCmdlet>(new Dictionary<string, object>
            {
                ["InputObject"] = new PSDeployment { DeploymentName = Piped },
                ["PassThru"] = true,
                ["AsJob"] = true,
            })));
            Assert.True(job.Finished.WaitOne(TimeSpan.FromSeconds(60)));
            Assert.Equal(JobState.Completed, job.JobStateInfo.State);
            Assert.True(Assert.IsType<bool>(Assert.Single(job.Output).BaseObject));
            LastSent(sent, "DELETE", DeploymentsPath + Piped + "?");

            int before = sent.Sent.Count;
            var error = Assert.Throws<InvalidOperationException>(() => host.RunPowerShellCmdlet<RemoveTenantDeploymentCmdlet>(
                new Dictionary<string, object> { ["InputObject"] = new PSDeployment() }));
            Assert.Contains("The deployment object has neither an Id nor a DeploymentName.", error.Message);
            Assert.Equal(before, sent.Sent.Count);
        });

    // T7 Tenant end-to-end with template parameters, tags, save and operations (scenario of the same title).
    // The surfaces send different requests, so each records its own steps.
    private static void EndToEndWithParametersTagsSaveAndOperations(RecordingSession recording)
    {
        RunScenario(recording, EndToEndCli, ps: null);
        RunScenario(recording, cli: null, EndToEndPowerShell);
    }

    // T8 Tenant what-if results (scenario of the same title).
    private static void TenantWhatIfResults(RecordingSession recording)
    {
        RunScenario(recording, WhatIfResultsCli, ps: null);
        RunScenario(recording, cli: null, WhatIfResultsPowerShell);
    }

    // T9 Preview a create with what-if (scenario of the same title). Both surfaces send only the what-if.
    private static void PreviewCreateWithWhatIf(RecordingSession recording) =>
        RunScenario(
            recording,
            (host, sent) => WithTemplateFile(file =>
            {
                host.RunCli("deployment", "tenant", "create", "--location", Location, "--name", PreviewName, "--template-file", file, "--what-if");
                AssertPreviewedOnly(sent);
            }),
            (host, sent) => WithTemplateFile(file =>
            {
                Assert.Empty(host.RunPowerShellCmdlet<NewTenantDeploymentCmdlet>(new Dictionary<string, object>
                {
                    ["Name"] = PreviewName,
                    ["Location"] = Location,
                    ["TemplateFile"] = file,
                    ["WhatIf"] = true,
                }));
                AssertPreviewedOnly(sent);
            }));

    private static void EndToEndCli(ScenarioHost host, RecordPlaybackInterceptor sent) => WithParameterTemplate(template =>
    {
        string[] parameters = ["--parameters", $"targetMG={ManagementGroup}", $"nestedSubId={SubscriptionId}"];

        var validated = Assert.IsType<DeploymentValidateResult>(Assert.Single(host.RunCli(
            ["deployment", "tenant", "validate", "--location", "WestUS", "--template-file", template, .. parameters])));
        Assert.Equal("Succeeded", validated.Properties?.ProvisioningState);
        Assert.Equal(ManagementGroup, TemplateParameter(LastSent(sent, "POST", DeploymentsPath + ParameterTemplateName + "/validate?"), "targetMG"));

        var created = Assert.IsType<DeploymentExtended>(Assert.Single(host.RunCli(
            ["deployment", "tenant", "create", "--location", "WestUS", "--name", EndToEndName, "--template-file", template, .. parameters])));
        Assert.Equal("Succeeded", created.ProvisioningState);
        Assert.Equal(SubscriptionId, TemplateParameter(LastSent(sent, "PUT", DeploymentsPath + EndToEndName + "?"), "nestedSubId"));

        var listed = Assert.IsType<List<DeploymentOperation>>(Assert.Single(host.RunCli(
            "deployment", "operation", "tenant", "list", "--name", EndToEndName)));
        Assert.Equal(4, listed.Count);

        var shown = Assert.IsType<List<DeploymentOperation>>(Assert.Single(host.RunCli(
            ["deployment", "operation", "tenant", "show", "--name", EndToEndName, "--operation-ids", .. listed.Select(o => o.OperationId!)])));
        Assert.Equal(
            [ProvisioningOperation.Create, ProvisioningOperation.Create, ProvisioningOperation.Create, ProvisioningOperation.EvaluateDeploymentOutput],
            shown.Select(o => o.Properties?.ProvisioningOperation));
        Assert.All(shown, o => Assert.Equal("Succeeded", o.Properties?.ProvisioningState));

        host.RunCli("deployment", "tenant", "delete", "--name", EndToEndName);
        LastSent(sent, "DELETE", DeploymentsPath + EndToEndName + "?");
    });

    private static void EndToEndPowerShell(ScenarioHost host, RecordPlaybackInterceptor sent) => WithParameterTemplate(template =>
    {
        // The template parameters bind as the cmdlet's dynamic parameters.
        var created = Assert.IsType<PSDeployment>(Assert.Single(host.RunPowerShellCmdlet<NewTenantDeploymentCmdlet>(new Dictionary<string, object>
        {
            ["Name"] = EndToEndPowerShellName,
            ["Location"] = "EastUS",
            ["TemplateFile"] = template,
            ["targetMG"] = ManagementGroup,
            ["nestedSubId"] = SubscriptionId,
            ["nestedRG"] = ResourceGroup,
            ["Tag"] = new Hashtable(s_tags),
        })));
        Assert.Equal("Succeeded", created.ProvisioningState);
        Assert.Equal(s_tags, created.Tags);
        CapturedHttpRequest put = LastSent(sent, "PUT", DeploymentsPath + EndToEndPowerShellName + "?");
        Assert.Equal(s_tags, Body(put).GetProperty("tags").Deserialize<Dictionary<string, string>>());
        Assert.Equal(ManagementGroup, TemplateParameter(put, "targetMG"));

        foreach (var get in new[]
        {
            new Dictionary<string, object> { ["Id"] = DeploymentsPath + EndToEndPowerShellName },
            new Dictionary<string, object> { ["Name"] = EndToEndPowerShellName },
        })
        {
            var deployment = Assert.IsType<PSDeployment>(Assert.Single(host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(get)));
            Assert.Equal(created.DeploymentName, deployment.DeploymentName);
            Assert.Equal(s_tags, deployment.Tags);
        }

        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var saved = Assert.IsType<DeploymentTemplateSaveResult>(Assert.Single(host.RunPowerShell("Save-AzTenantDeploymentTemplate", new Dictionary<string, object>
            {
                ["Name"] = EndToEndPowerShellName,
                ["Path"] = directory,
                ["Force"] = true,
            })));
            Assert.False(string.IsNullOrEmpty(saved.Path));
            Assert.True(File.Exists(saved.Path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        Assert.Equal(4, host.RunPowerShellCmdlet<GetTenantDeploymentOperationCmdlet>(
            new Dictionary<string, object> { ["DeploymentName"] = EndToEndPowerShellName }).Count);

        host.RunPowerShell("Remove-AzTenantDeployment", new Dictionary<string, object> { ["Name"] = EndToEndPowerShellName });
        LastSent(sent, "DELETE", DeploymentsPath + EndToEndPowerShellName + "?");
    });

    private static void WhatIfResultsCli(ScenarioHost host, RecordPlaybackInterceptor sent) => WithParameterTemplate(template =>
    {
        var result = Assert.IsType<WhatIfOperationResult>(Assert.Single(host.RunCli(
            "deployment", "tenant", "what-if", "--location", "WestUS", "--template-file", template,
            "--parameters", $"targetMG={ManagementGroup}", $"nestedSubId={SubscriptionId}", "--no-pretty-print")));
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal(3, result.Properties?.Changes?.Count);
        Assert.All(result.Properties!.Changes!, c => Assert.Equal("Create", c.ChangeType));
    });

    private static void WhatIfResultsPowerShell(ScenarioHost host, RecordPlaybackInterceptor sent) => WithParameterTemplate(template =>
    {
        WhatIfOperationResult WhatIf(Dictionary<string, object> extra)
        {
            var parameters = new Dictionary<string, object>(extra)
            {
                ["Name"] = WhatIfName,
                ["Location"] = "westus",
                ["TemplateFile"] = template,
                ["targetMG"] = ManagementGroup,
                ["nestedSubId"] = SubscriptionId,
                ["nestedRG"] = ResourceGroup,
            };
            var result = Assert.IsType<WhatIfOperationResult>(Assert.Single(host.RunPowerShellCmdlet<GetTenantDeploymentWhatIfResultCmdlet>(parameters)));
            Assert.Equal("Succeeded", result.Status);
            return result;
        }
        string whatIfPath = DeploymentsPath + WhatIfName + "/whatIf?";

        List<WhatIfChange> changes = WhatIf(new()).Properties!.Changes!;
        Assert.NotEmpty(changes);
        Assert.All(changes, c => Assert.Equal("Create", c.ChangeType));

        changes = WhatIf(new() { ["ResultFormat"] = "ResourceIdOnly" }).Properties!.Changes!;
        Assert.All(changes, c =>
        {
            Assert.False(string.IsNullOrEmpty(c.ResourceId));
            Assert.Null(c.Before);
            Assert.Null(c.After);
            Assert.Null(c.Delta);
        });
        Assert.Equal("ResourceIdOnly", Body(LastSent(sent, "POST", whatIfPath))
            .GetProperty("properties").GetProperty("whatIfSettings").GetProperty("resultFormat").GetString());

        Assert.Empty(WhatIf(new() { ["ResultFormat"] = "ResourceIdOnly", ["ExcludeChangeType"] = new[] { "Create" } }).Properties!.Changes!);

        changes = WhatIf(new() { ["ValidationLevel"] = "ProviderNoRbac" }).Properties!.Changes!;
        Assert.All(changes, c => Assert.Equal("Create", c.ChangeType));
        Assert.Equal("ProviderNoRbac", ValidationLevel(LastSent(sent, "POST", whatIfPath)));
    });

    private static void AssertPreviewedOnly(RecordPlaybackInterceptor sent)
    {
        LastSent(sent, "POST", DeploymentsPath + PreviewName + "/whatIf?");
        Assert.DoesNotContain(sent.Sent, r => r.Method == "PUT");
    }

    private static string? TemplateParameter(CapturedHttpRequest request, string name) =>
        Body(request).GetProperty("properties").GetProperty("parameters").GetProperty(name).GetProperty("value").GetString();

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
        PSDeployment created = null!;
        WithTemplateFile(template =>
        {
            var body = new Dictionary<string, object>
            {
                ["Name"] = DeploymentName,
                ["Location"] = Location,
                ["TemplateFile"] = template,
            };
            Assert.Empty(host.RunPowerShell("Test-AzTenantDeployment", body));
            created = Assert.IsType<PSDeployment>(Assert.Single(host.RunPowerShellCmdlet<NewTenantDeploymentCmdlet>(body)));
        });
        Assert.Equal("Succeeded", created.ProvisioningState);

        Assert.Equal(created.DeploymentName, Assert.IsType<PSDeployment>(Assert.Single(host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(
            new Dictionary<string, object> { ["Id"] = DeploymentsPath + DeploymentName }))).DeploymentName);
        Assert.Equal(created.DeploymentName, Assert.IsType<PSDeployment>(Assert.Single(host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(
            new Dictionary<string, object> { ["Name"] = DeploymentName }))).DeploymentName);

        Assert.Contains(host.RunPowerShellCmdlet<GetTenantDeploymentCmdlet>(new Dictionary<string, object>()),
            o => Assert.IsType<PSDeployment>(o).DeploymentName == DeploymentName);
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
        Assert.Empty(host.RunPowerShell("Test-AzTenantDeployment", new Dictionary<string, object>
        {
            ["Name"] = "scenario-tenant-validate",
            ["Location"] = Location,
            ["TemplateFile"] = template,
            ["ValidationLevel"] = "Template",
        }));
        Assert.Equal("Template", ValidationLevel(LastSent(sent, "POST", DeploymentsPath + "scenario-tenant-validate/validate?")));

        var created = Assert.IsType<PSDeployment>(Assert.Single(host.RunPowerShell("New-AzTenantDeployment", new Dictionary<string, object>
        {
            ["Location"] = Location,
            ["TemplateFile"] = template,
            ["ValidationLevel"] = "Provider",
        })));
        Assert.Equal("Succeeded", created.ProvisioningState);
        Assert.Equal("Provider", ValidationLevel(LastSent(sent, "PUT", DeploymentsPath + "empty_template_tenant?")));
    });

    private static void AssertValidated(IReadOnlyList<object> output, RecordPlaybackInterceptor sent)
    {
        Assert.Equal("Succeeded", Assert.IsType<DeploymentValidateResult>(Assert.Single(output)).Properties?.ProvisioningState);
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
    private static void WithDerivedNameTemplate(Action<string> run) => WithNamedTemplate("empty_template_tenant.json", s_template, run);

    private static void WithParameterTemplate(Action<string> run) => WithNamedTemplate(ParameterTemplateName + ".json", s_parameterTemplate, run);

    private static void WithNamedTemplate(string fileName, Dictionary<string, object> content, Action<string> run)
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string template = Path.Combine(directory, fileName);
            File.WriteAllText(template, JsonSerializer.Serialize(content));
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
                bool idsOnly = ReadBody(request).Contains("\"ResourceIdOnly\"", StringComparison.Ordinal);
                string changes = string.Join(",", s_whatIfResourceIds.Select(id => idsOnly
                    ? $$$"""{"resourceId":"{{{id}}}","changeType":"Create"}"""
                    : $$$"""{"resourceId":"{{{id}}}","changeType":"Create","after":{"id":"{{{id}}}"}}"""));
                return Task.FromResult(Json($$$"""{"status":"Succeeded","properties":{"changes":[{{{changes}}}]}}"""));
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
                return Task.FromResult(Json(
                    name == FailedName ? FailedOperations
                    : name.StartsWith(EndToEndName, StringComparison.OrdinalIgnoreCase)
                        ? $$"""{"value":[{{string.Join(",", s_endToEndOperations.Select(o => Operation(name, o)))}}]}"""
                    : """{"value":[]}"""));
            }

            if (request.Method == HttpMethod.Get && path.Contains("/operations/", StringComparison.OrdinalIgnoreCase))
            {
                string operationId = path[(path.LastIndexOf('/') + 1)..];
                return Task.FromResult(Json(Operation(name, s_endToEndOperations.Single(o => o.Id == operationId))));
            }

            if (request.Method == HttpMethod.Head)
            {
                return Task.FromResult(new HttpResponseMessage(_deleted ? HttpStatusCode.NotFound : HttpStatusCode.NoContent));
            }

            if (request.Method == HttpMethod.Put)
            {
                using var body = JsonDocument.Parse(ReadBody(request));
                if (body.RootElement.TryGetProperty("tags", out JsonElement tags) && tags.ValueKind == JsonValueKind.Object)
                {
                    _tags[name] = tags.GetRawText();
                }
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

        private static readonly string[] s_whatIfResourceIds =
        [
            $"/providers/Microsoft.Management/managementGroups/{ManagementGroup}",
            $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroup}",
            $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroup}/providers/Microsoft.Storage/storageAccounts/scenariostorage",
        ];

        private static readonly (string Id, string Operation, string? Target)[] s_endToEndOperations =
        [
            ("op-mg", "Create", s_whatIfResourceIds[0]),
            ("op-rg", "Create", s_whatIfResourceIds[1]),
            ("op-storage", "Create", s_whatIfResourceIds[2]),
            ("op-output", "EvaluateDeploymentOutput", null),
        ];

        private readonly Dictionary<string, string> _tags = new(StringComparer.OrdinalIgnoreCase);

        private static string Operation(string name, (string Id, string Operation, string? Target) operation)
        {
            string target = operation.Target is null ? "" : $$""","targetResource":{"id":"{{operation.Target}}"}""";
            return $$$"""
            {"id":"{{{DeploymentsPath}}}{{{name}}}/operations/{{{operation.Id}}}","operationId":"{{{operation.Id}}}",
             "properties":{"provisioningOperation":"{{{operation.Operation}}}","provisioningState":"Succeeded","statusCode":"OK"{{{target}}}}}
            """;
        }

        private static string ReadBody(HttpRequestMessage request) =>
            request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "{}";

        private string Deployment(string name, bool canceled)
        {
            bool failed = name.StartsWith(FailedName, StringComparison.OrdinalIgnoreCase);
            string state = failed ? "Failed" : canceled ? "Canceled" : "Succeeded";
            string error = name == FailedNoOpsName
                ? ""","error":{"code":"ScenarioDeploymentError","message":"Scenario deployment failed."}"""
                : "";
            string tags = _tags.TryGetValue(name, out string? raw) ? $$""","tags":{{raw}}""" : "";
            return $$"""
            {
              "id": "{{DeploymentsPath}}{{name}}",
              "name": "{{name}}",
              "type": "Microsoft.Resources/deployments",
              "location": "{{Location}}"{{tags}},
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
