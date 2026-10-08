// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Xunit;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public class ManagementGroupDeploymentTests
{
    [Fact]
    public void HttpRuntime_PreservesSevenArgumentSendAsyncLROOverload()
    {
        MethodInfo? overload = typeof(HttpRuntime).GetMethod(
            nameof(HttpRuntime.SendAsyncLRO),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            [
                typeof(Func<Task<HttpRequestMessage>>),
                typeof(IEventPublisher),
                typeof(IRuntimeHttpPipeline),
                typeof(Func<HttpResponseMessage, string?>),
                typeof(Func<HttpResponseMessage, string?>),
                typeof(Func<HttpResponseMessage, string?>),
                typeof(Func<HttpResponseMessage, string?>),
            ],
            modifiers: null);

        Assert.NotNull(overload);
    }

    private static void Init(
        IProcessor processor,
        FakeHttpPipeline pipeline,
        FakeOutputHandler output,
        IEventPublisher? publisher = null)
    {
        pipeline.PollingInterval = 0;
        Profile profile = new() { TenantId = "test-tenant", SubscriptionId = "test-subscription" };
        processor.Init(new ProcessorContext(
            pipeline,
            publisher ?? new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string RequestUri(IProcessor processor) =>
        (string)processor.GetType()
            .GetMethod("CreateRequestUri", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(processor, null)!;

    [Theory]
    [InlineData(typeof(CancelAtManagementGroupScopeDeploymentsProcessor), "/cancel?")]
    [InlineData(typeof(CreateOrUpdateAtManagementGroupScopeDeploymentsProcessor), "?api-version")]
    [InlineData(typeof(DeleteAtManagementGroupScopeDeploymentsProcessor), "?api-version")]
    [InlineData(typeof(ExportTemplateAtManagementGroupScopeDeploymentsProcessor), "/exportTemplate?")]
    [InlineData(typeof(GetAtManagementGroupScopeDeploymentsProcessor), "?api-version")]
    [InlineData(typeof(ValidateAtManagementGroupScopeDeploymentsProcessor), "/validate?")]
    [InlineData(typeof(WhatIfAtManagementGroupScopeDeploymentsProcessor), "/whatIf?")]
    public void ScopeAndDeploymentNames_AreEscapedAsPathSegments(Type processorType, string suffix)
    {
        var processor = (IProcessor)Activator.CreateInstance(processorType)!;
        processorType.GetProperty("GroupId")!.SetValue(processor, "group/with?reserved#chars");
        processorType.GetProperty("DeploymentName")!.SetValue(processor, "name/with?reserved#chars");

        Assert.Contains(
            "/managementGroups/group%2Fwith%3Freserved%23chars/providers/Microsoft.Resources/" +
            $"deployments/name%2Fwith%3Freserved%23chars{suffix}",
            RequestUri(processor));
    }

    [Fact]
    public void List_ScopeAndFilter_AreEncoded()
    {
        var processor = new ListAtManagementGroupScopeDeploymentsProcessor
        {
            GroupId = "group/with?reserved#chars",
            _filter = "provisioningState eq 'Succeeded' & name eq 'a/b'",
        };
        string uri = RequestUri(processor);

        Assert.Contains("/managementGroups/group%2Fwith%3Freserved%23chars/", uri);
        Assert.Contains(
            "$filter=provisioningState%20eq%20%27Succeeded%27%20%26%20name%20eq%20%27a%2Fb%27",
            uri);
    }

    [Fact]
    public void ExtensionIndex_ExposesRequestedManagementGroupCommands()
    {
        var index = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex();

        Assert.Equal(
            [
                "deployment mg cancel",
                "deployment mg create",
                "deployment mg delete",
                "deployment mg export",
                "deployment mg list",
                "deployment mg show",
                "deployment mg validate",
                "deployment mg wait",
                "deployment mg what-if",
            ],
            index.GetCommandKeys(ClientType.CLI)
                .Where(key => key.StartsWith("deployment mg ", StringComparison.Ordinal))
                .Order()
                .ToArray());

        foreach (string command in new[]
        {
            "deployment mg create",
            "deployment mg delete",
            "deployment mg validate",
            "deployment mg what-if",
        })
        {
            CommandMetadata metadata = index.GetCommandMetadata(command, ClientType.CLI);
            Assert.DoesNotContain("no-wait", metadata.Parameters.Keys);
            Assert.DoesNotContain("no-prompt", metadata.Parameters.Keys);
            Assert.DoesNotContain("no-pretty-print", metadata.Parameters.Keys);
            Assert.DoesNotContain("no-color", metadata.Parameters.Keys);
        }

        // `--mode` is configurable only on `create` (matches upstream `az deployment mg create
        // --mode`). `validate`/`what-if` take no `mode` argument upstream and always behave as
        // Incremental, so Azclips must not expose or honor `--mode` on those two commands.
        CommandMetadata createMetadataForMode = index.GetCommandMetadata("deployment mg create", ClientType.CLI);
        Assert.Contains("mode", createMetadataForMode.Parameters.Keys);
        Assert.Equal(["Incremental", "Complete"], createMetadataForMode.Parameters["mode"].AllowedValues);

        foreach (string command in new[] { "deployment mg validate", "deployment mg what-if" })
        {
            CommandMetadata metadata = index.GetCommandMetadata(command, ClientType.CLI);
            Assert.DoesNotContain("mode", metadata.Parameters.Keys);
        }

        CommandMetadata createCli = index.GetCommandMetadata("deployment mg create", ClientType.CLI);
        Assert.Contains("what-if", createCli.Parameters.Keys);
        Assert.Contains("confirm-with-what-if", createCli.Parameters.Keys);
        Assert.Contains("proceed-if-no-change", createCli.Parameters.Keys);
        Assert.Contains("what-if-exclude-change-types", createCli.Parameters.Keys);

        Assert.Equal(
            [
                "Get-AzManagementGroupDeployment_Get",
                "Get-AzManagementGroupDeployment_List",
                "New-AzManagementGroupDeployment",
                "Remove-AzManagementGroupDeployment",
                "Stop-AzManagementGroupDeployment",
                "Test-AzManagementGroupDeployment",
            ],
            index.GetCommandKeys(ClientType.PowerShell)
                .Where(key => key.Contains("ManagementGroupDeployment", StringComparison.Ordinal))
                .Order()
                .ToArray());

        Assert.All(
            index.GetCommandKeys(ClientType.PowerShell)
                .Where(key => key.Contains("ManagementGroupDeployment", StringComparison.Ordinal)),
            command => Assert.Contains(
                "Profile",
                index.GetCommandMetadata(command, ClientType.PowerShell).Parameters.Keys));

        CommandMetadata createPs =
            index.GetCommandMetadata("New-AzManagementGroupDeployment", ClientType.PowerShell);
        Assert.Equal("ParametersFile", createPs.Parameters["TemplateParameterFile"].Name);
        Assert.Equal("ParametersUri", createPs.Parameters["TemplateParameterUri"].Name);
        Assert.Equal("ParametersObject", createPs.Parameters["TemplateParameterObject"].Name);
        Assert.Equal("Tags", createPs.Parameters["Tag"].Name);

        CommandMetadata getPs =
            index.GetCommandMetadata("Get-AzManagementGroupDeployment_Get", ClientType.PowerShell);
        Assert.Equal("GroupId", getPs.Parameters["ManagementGroupId"].Name);
        Assert.Equal("DeploymentName", getPs.Parameters["Name"].Name);

        CommandMetadata removePs =
            index.GetCommandMetadata("Remove-AzManagementGroupDeployment", ClientType.PowerShell);
        Assert.Equal("GroupId", removePs.Parameters["ManagementGroupId"].Name);
        Assert.Equal("DeploymentName", removePs.Parameters["Name"].Name);
    }

    [Fact]
    public async Task Create_ValidatesThenSendsScopedBody()
    {
        List<(HttpMethod Method, Uri Uri, string? Body)> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((
                request.Method,
                request.RequestUri!,
                request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()));
            return requests.Count == 1
                ? Json("""{ "name": "mg-test" }""")
                : Json("""{ "name": "mg-test", "location": "eastus", "properties": { "provisioningState": "Succeeded" } }""");
        });
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object>
            {
                ["resources"] = Array.Empty<object>(),
            },
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(4, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.EndsWith("/validate?api-version=2025-04-01", requests[0].Uri.ToString());
        Assert.Equal(HttpMethod.Put, requests[1].Method);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Management/managementGroups/contoso/providers/Microsoft.Resources/deployments/mg-test?api-version=2025-04-01",
            requests[1].Uri.ToString());

        JsonElement body = JsonDocument.Parse(requests[1].Body!).RootElement;
        Assert.Equal("eastus", body.GetProperty("location").GetString());
        Assert.Equal("Incremental", body.GetProperty("properties").GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Create_ForwardsConfiguredMode()
    {
        // `--mode Complete` must reach the deployment body instead of always being hardcoded to
        // Incremental (matches upstream `az deployment mg create --mode`).
        List<string?> bodies = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            bodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return bodies.Count == 1
                ? Json("{}")
                : Json("""{ "name": "mg-test", "properties": { "provisioningState": "Succeeded" } }""");
        });
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            Mode = "Complete",
            TemplateObject = new Dictionary<string, object> { ["resources"] = Array.Empty<object>() },
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        JsonElement body = JsonDocument.Parse(bodies[1]!).RootElement;
        Assert.Equal("Complete", body.GetProperty("properties").GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Validate_AlwaysUsesIncrementalModeEvenWhenConfigured()
    {
        // Upstream `validate_arm_template_at_management_group` takes no `mode` argument and
        // always hardcodes `mode='Incremental'`; a configured Mode must not leak through.
        List<string?> bodies = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            bodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return Json("""{ "status": "Succeeded" }""");
        });
        var processor = new ValidateManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            Mode = "Complete",
            TemplateObject = new Dictionary<string, object> { ["resources"] = Array.Empty<object>() },
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        JsonElement body = JsonDocument.Parse(bodies[0]!).RootElement;
        Assert.Equal("Incremental", body.GetProperty("properties").GetProperty("mode").GetString());
    }

    [Fact]
    public async Task WhatIf_AlwaysUsesIncrementalModeEvenWhenConfigured()
    {
        // Upstream `what_if_deploy_arm_template_at_management_group` takes no `mode` argument and
        // always hardcodes `DeploymentMode.incremental`; a configured Mode must not leak through.
        List<string?> bodies = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            bodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return Json("""{ "status": "Succeeded", "properties": { "changes": [] } }""");
        });
        var processor = new ManagementGroupDeploymentWhatIfProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            Mode = "Complete",
            TemplateObject = new Dictionary<string, object> { ["resources"] = Array.Empty<object>() },
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        JsonElement body = JsonDocument.Parse(bodies[0]!).RootElement;
        Assert.Equal("Incremental", body.GetProperty("properties").GetProperty("mode").GetString());
    }

    [Fact]
    public async Task CreateWhatIfPreview_UsesIncrementalModeEvenWhenCreateModeIsComplete()
    {
        // The `--what-if`/`--confirm-with-what-if` preview inside `create` must still hardcode
        // Incremental for the preview call, even though the eventual create honors `--mode`.
        List<string?> bodies = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            bodies.Add(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult());
            return Json("""{ "status": "Succeeded", "properties": { "changes": [] } }""");
        });
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            Mode = "Complete",
            WhatIf = true,
            TemplateObject = new Dictionary<string, object> { ["resources"] = Array.Empty<object>() },
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        JsonElement body = JsonDocument.Parse(bodies[0]!).RootElement;
        Assert.Equal("Incremental", body.GetProperty("properties").GetProperty("mode").GetString());
    }

    [Fact]
    public async Task ConfirmWithWhatIf_ProceedIfNoChangeAndNoChanges_SkipsPromptAndCreates()
    {
        // Matches upstream's `not proceed_if_no_change or has_change` gate: when
        // `-ProceedIfNoChange` is set and what-if reports only `NoChange`/`Ignore`, the
        // confirmation prompt must be skipped entirely and the deployment proceeds straight to
        // validate+create without touching the console.
        List<HttpMethod> methods = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            methods.Add(request.Method);
            return methods.Count switch
            {
                1 => Json(
                    """{ "status": "Succeeded", "properties": { "changes": [ { "changeType": "NoChange" } ] } }"""),
                2 => Json("""{ "name": "mg-test" }"""),
                _ => Json(
                    """{ "name": "mg-test", "properties": { "provisioningState": "Succeeded" } }"""),
            };
        });
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            ConfirmWithWhatIf = true,
            ProceedIfNoChange = true,
            TemplateObject = new Dictionary<string, object> { ["resources"] = Array.Empty<object>() },
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        // what-if, then preflight validate, then the actual create (plus provisioning-state
        // polling) — no console prompt in between.
        Assert.Equal(5, methods.Count);
    }

    [Fact]
    public void PrepareAndValidate_DerivesDefaultDeploymentNameFromTemplateFile()
    {
        // Matches `az deployment <scope> create`'s `_validate_deployment_name`: default to the
        // local template file's base name when no name is supplied.
        string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        File.WriteAllText(tempFile, """{ "resources": [] }""");
        try
        {
            var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
            {
                ManagementGroupId = "contoso",
                Location = "eastus",
                TemplateFile = tempFile,
            };
            MethodInfo method = processor.GetType().GetMethod(
                "PrepareAndValidate", BindingFlags.Instance | BindingFlags.NonPublic)!;

            method.Invoke(processor, null);

            Assert.Equal(Path.GetFileNameWithoutExtension(tempFile), processor.Name);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void PrepareAndValidate_FallsBackToLiteralDeploymentNameWhenUnresolvable()
    {
        // A template spec ID that trims down to no resolvable resource-name segment (and no
        // template file/URI): falls back to the literal "deployment1", matching
        // `az deployment <scope> create`'s default.
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.Initialize(
            new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Location = "eastus",
            TemplateSpecId = "///",
        };
        MethodInfo method = processor.GetType().GetMethod(
            "PrepareAndValidate", BindingFlags.Instance | BindingFlags.NonPublic)!;

        method.Invoke(processor, null);

        Assert.Equal("deployment1", processor.Name);
    }

    [Fact]
    public void PrepareAndValidate_DerivesDefaultDeploymentNameFromTemplateSpecId()
    {
        // Matches `az deployment <scope> create`'s `_validate_deployment_name_with_template_specs`:
        // default to the template spec ID's last path segment when only `--template-spec` is
        // given (no "/versions/x.y" suffix, so the last segment is the spec's own resource name).
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.Initialize(
            new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Location = "eastus",
            TemplateSpecId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg1"
                + "/providers/Microsoft.Resources/templateSpecs/myTemplateSpec",
        };
        MethodInfo method = processor.GetType().GetMethod(
            "PrepareAndValidate", BindingFlags.Instance | BindingFlags.NonPublic)!;

        method.Invoke(processor, null);

        Assert.Equal("myTemplateSpec", processor.Name);
    }

    [Fact]
    public void PrepareAndValidate_GeneratesUniqueDeploymentNameFromTemplateObject()
    {
        // Matches Azure PowerShell's `NewResourceManagerSdkClient.GenerateDeploymentName`: when
        // only an inline template object is given (PowerShell's `-TemplateObject`), generate a
        // fresh unique name per invocation instead of reusing a fixed literal. (CLI clients fall
        // back to the literal "deployment1" instead; this test exercises the PowerShell client.)
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.Initialize(
            new ClientMetadata(ClientType.PowerShell, "00000000-0000-0000-0000-000000000000"));
        var first = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object> { ["resources"] = Array.Empty<object>() },
        };
        var second = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object> { ["resources"] = Array.Empty<object>() },
        };
        MethodInfo method = first.GetType().GetMethod(
            "PrepareAndValidate", BindingFlags.Instance | BindingFlags.NonPublic)!;

        method.Invoke(first, null);
        method.Invoke(second, null);

        Assert.NotEqual("deployment1", first.Name);
        Assert.NotEqual(first.Name, second.Name);
        Assert.True(Guid.TryParse(first.Name, out _));
    }

    [Fact]
    public void PrepareAndValidate_DerivesDefaultDeploymentNameFromTemplateUri()
    {
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Location = "eastus",
            TemplateUri = "https://example.com/templates/azuredeploy.json",
        };
        MethodInfo method = processor.GetType().GetMethod(
            "PrepareAndValidate", BindingFlags.Instance | BindingFlags.NonPublic)!;

        method.Invoke(processor, null);

        Assert.Equal("azuredeploy", processor.Name);
    }

    [Theory]
    [InlineData("managementGroupDeploymentTemplate.json", "managementGroup", true)]
    [InlineData("deploymentTemplate.json", "resourceGroup", false)]
    [InlineData("tenantDeploymentTemplate.json", "tenant", false)]
    public void ValidateBicepTargetScope_RejectsSchemaMismatchedWithManagementGroupScope(
        string schemaFile, string impliedScope, bool matchesManagementGroupScope)
    {
        // Compiled Bicep carries its target scope in "$schema"; a mismatch against the command's
        // scope should fail fast instead of being deferred to a less specific ARM error.
        using JsonDocument compiled = JsonDocument.Parse(
            $$"""{ "$schema": "https://schema.management.azure.com/schemas/2019-08-01/{{schemaFile}}#", "resources": [] }""");
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator();
        MethodInfo method = typeof(ManagementGroupDeploymentOrchestratorBase<ScopedDeployment, DeploymentExtended>)
            .BaseType!
            .GetMethod("ValidateBicepTargetScope", BindingFlags.Instance | BindingFlags.NonPublic)!;

        if (matchesManagementGroupScope)
        {
            method.Invoke(processor, [compiled.RootElement]);
            return;
        }

        TargetInvocationException invocationException = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(processor, [compiled.RootElement]));
        ArgumentException error = Assert.IsType<ArgumentException>(invocationException.InnerException);
        Assert.Contains(impliedScope, error.Message);
        Assert.Contains("managementGroup", error.Message);
    }

    [Fact]
    public void BuildParameters_MergesFileAndInlineSourcesWithTemplateTypedValues()
    {
        // Gap #1: parameters must merge multiple sources (a parameters object plus inline
        // overlays) and coerce inline KEY=VALUE tokens using the template's declared parameter
        // types, matching upstream's parameter-resolution behavior.
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            // NormalizeParametersObject() expects the flat, already-unwrapped shape (matching
            // PowerShell's -TemplateParameterObject), not the ARM-file "parameters" envelope.
            ParametersObject = new Dictionary<string, object>
            {
                ["fromFile"] = new Dictionary<string, object> { ["value"] = "file-value" },
            },
            Parameter = ["count=3", "flag=true", "name=01234"],
            TemplateObject = JsonDocument.Parse(
                """
                {
                  "parameters": {
                    "fromFile": { "type": "string" },
                    "count": { "type": "int" },
                    "flag": { "type": "bool" },
                    "name": { "type": "string" }
                  },
                  "resources": []
                }
                """).RootElement,
        };
        MethodInfo method = processor.GetType().GetMethod(
            "BuildDeployment", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var deployment = Assert.IsType<ScopedDeployment>(method.Invoke(processor, [Type.Missing]));

        JsonObject parameters = Assert.IsType<JsonObject>(deployment.Properties.Parameters);
        Assert.Equal("file-value", parameters["fromFile"]!["value"]!.GetValue<string>());
        Assert.Equal(3L, parameters["count"]!["value"]!.GetValue<long>());
        Assert.True(parameters["flag"]!["value"]!.GetValue<bool>());
        // "name" is typed as "string" by the template, so the JSON-parseable "01234" is still
        // coerced to the string "01234" rather than left as (invalid) JSON.
        Assert.Equal("01234", parameters["name"]!["value"]!.GetValue<string>());
    }

    [Fact]
    public async Task Wait_CustomCondition_SupportsGeneralJmesPathBeyondProvisioningState()
    {
        // Gap #6: `--custom` must accept any JMESPath expression, not just the hardcoded
        // "properties.provisioningState==<value>" prefix form.
        var pipeline = new FakeHttpPipeline((_, _) => Json(
            """{ "name": "mg-test", "properties": { "provisioningState": "Succeeded", "outputs": { "result": { "value": "ready" } } } }"""));
        var processor = new WaitManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Custom = "properties.outputs.result.value=='ready'",
            Interval = 1,
            Timeout = 5,
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
    }

    [Theory]
    [InlineData("RequestContent", "RequestContent")]
    [InlineData("ResponseContent", "ResponseContent")]
    [InlineData("All", "RequestContent,ResponseContent")]
    [InlineData("None", null)]
    public void Create_NormalizesPowerShellDebugLevel(string input, string? expected)
    {
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            TemplateObject = new Dictionary<string, object> { ["resources"] = Array.Empty<object>() },
            DeploymentDebugLogLevel = input,
        };
        MethodInfo method = processor.GetType().GetMethod(
            "BuildDeployment",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var deployment = Assert.IsType<ScopedDeployment>(method.Invoke(processor, [Type.Missing]));

        Assert.Equal(expected, deployment.Properties.DebugSetting?.DetailLevel);
    }

    [Fact]
    public async Task List_UnwrapsManagementGroupDeploymentPage()
    {
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            Assert.Contains("/managementGroups/contoso/", request.RequestUri!.ToString());
            return Json("""
                {
                  "value": [
                    { "name": "one", "properties": { "provisioningState": "Succeeded" } },
                    { "name": "two", "properties": { "provisioningState": "Failed" } }
                  ]
                }
                """);
        });
        var output = new FakeOutputHandler();
        var processor = new ListAtManagementGroupScopeDeploymentsProcessor
        {
            GroupId = "contoso",
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        var values = Assert.IsType<List<DeploymentExtended>>(Assert.Single(output.WrittenValues));
        Assert.Equal(["one", "two"], values.Select(value => value.Name));
    }

    [Fact]
    public async Task Stop_SendsGetPrecheckThenCancelWhenDeploymentIsRunning()
    {
        // Current design adds a safety pre-check: GET the deployment first and only cancel when
        // it isn't already in a terminal (Succeeded/Failed) provisioning state.
        List<(HttpMethod Method, Uri Uri)> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!));
            return requests.Count switch
            {
                1 => Json(
                    """{ "name": "active", "properties": { "provisioningState": "Running" } }"""),
                2 => Json("{}"),
                _ => throw new InvalidOperationException("Unexpected request."),
            };
        });
        var processor = new StopManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "active",
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(2, requests.Count);
        Assert.Equal(HttpMethod.Get, requests[0].Method);
        Assert.Equal(HttpMethod.Post, requests[1].Method);
        Assert.Contains("/cancel?api-version", requests[1].Uri.ToString());
    }

    [Fact]
    public async Task Stop_ThrowsWhenDeploymentAlreadyInTerminalState()
    {
        var pipeline = new FakeHttpPipeline((_, _) =>
            Json("""{ "name": "done", "properties": { "provisioningState": "Succeeded" } }"""));
        var processor = new StopManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "done",
        };
        Init(processor, pipeline, new FakeOutputHandler());

        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());

        Assert.Contains("No running deployments", exception.Message);
        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task PowerShellTest_ReturnsValidationError()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json(
            """
            {
              "error": {
                "code": "InvalidTemplate",
                "message": "The template is invalid."
              }
            }
            """));
        var output = new FakeOutputHandler();
        var processor = new TestManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "invalid",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object>
            {
                ["resources"] = Array.Empty<object>(),
            },
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        var result = Assert.IsType<DeploymentValidateResult>(Assert.Single(output.WrittenValues));
        Assert.Equal("InvalidTemplate", result.Error?.Code);
    }

    [Fact]
    public async Task CliValidate_ThrowsValidationError()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json(
            """
            {
              "error": {
                "code": "InvalidTemplate",
                "message": "The template is invalid."
              }
            }
            """));
        var processor = new ValidateManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "invalid",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object>
            {
                ["resources"] = Array.Empty<object>(),
            },
        };
        Init(processor, pipeline, new FakeOutputHandler());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());

        Assert.Contains("InvalidTemplate", exception.Message);
    }

    [Fact]
    public async Task ValidateAsync_SendsSingleRequestWithoutPolling()
    {
        // Test-AzManagementGroupDeployment (ThrowOnError = false) reads the immediate response
        // body directly instead of following Location for the final polled result — matching
        // the equivalent Test-AzTenantDeployment design.
        List<(HttpMethod Method, Uri Uri)> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!));
            return Json(
                """{ "name": "validated", "properties": { "provisioningState": "Succeeded" } }""");
        });
        var processor = new TestManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "validated",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object>
            {
                ["resources"] = Array.Empty<object>(),
            },
        };
        var output = new FakeOutputHandler();
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.EndsWith("/validate?api-version=2025-04-01", requests[0].Uri.ToString());
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task WhatIfAsync_FollowsGeneratedFinalLocation()
    {
        List<(HttpMethod Method, Uri Uri)> requests = [];
        const string location =
            "https://management.azure.com/providers/Microsoft.Resources/operations/whatif-1";
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!));
            return requests.Count switch
            {
                1 => WithLocation(
                    Json("""{ "status": "Accepted" }""", HttpStatusCode.Accepted),
                    location),
                2 => Json(
                    """{ "status": "Succeeded", "properties": { "changes": [] } }"""),
                3 => Json(
                    """{ "status": "Succeeded", "properties": { "changes": [] } }"""),
                _ => throw new InvalidOperationException("Unexpected request."),
            };
        });
        var processor = new ManagementGroupDeploymentWhatIfProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "whatif",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object>
            {
                ["resources"] = Array.Empty<object>(),
            },
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(3, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.EndsWith("/whatIf?api-version=2025-04-01", requests[0].Uri.ToString());
        Assert.Equal(HttpMethod.Get, requests[1].Method);
        Assert.Equal(location, requests[1].Uri.ToString());
        Assert.Equal(requests[1], requests[2]);
    }

    [Fact]
    public async Task WhatIfAsync_StatusFailedWithoutErrorDoesNotThrow()
    {
        // ThrowIfError() only inspects the structured `error` object; a bare top-level
        // `status: "Failed"` with no `error` is not treated as a failure.
        var pipeline = new FakeHttpPipeline((_, _) =>
            Json("""{ "status": "Failed", "properties": { "changes": [] } }"""));
        var processor = new ManagementGroupDeploymentWhatIfProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "whatif",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object>
            {
                ["resources"] = Array.Empty<object>(),
            },
        };
        var output = new FakeOutputHandler();
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task CreateWhatIf_StatusFailedWithoutErrorDoesNotThrow()
    {
        var pipeline = new FakeHttpPipeline((_, _) =>
            Json("""{ "status": "Failed", "properties": { "changes": [] } }"""));
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "whatif",
            Location = "eastus",
            WhatIf = true,
            TemplateObject = new Dictionary<string, object>
            {
                ["resources"] = Array.Empty<object>(),
            },
        };
        var output = new FakeOutputHandler();
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task Delete_WaitsOnLocationWithoutWritingOperationPayload()
    {
        List<Uri> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add(request.RequestUri!);
            return requests.Count switch
            {
                1 => WithLocation(
                    Json("""{ "status": "Accepted" }""", HttpStatusCode.Accepted),
                    "https://management.azure.com/providers/Microsoft.Resources/operations/delete-1"),
                2 => new HttpResponseMessage(HttpStatusCode.NoContent),
                3 => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => throw new InvalidOperationException("Unexpected request."),
            };
        });
        var output = new FakeOutputHandler();
        var processor = new DeleteAtManagementGroupScopeDeploymentsProcessor
        {
            GroupId = "contoso",
            DeploymentName = "mg-test",
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(3, requests.Count);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Resources/operations/delete-1",
            requests[1].ToString());
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task WaitDeleted_TreatsNotFoundAsSuccess()
    {
        var pipeline = new FakeHttpPipeline((_, _) =>
            Json("""{ "error": { "code": "NotFound" } }""", HttpStatusCode.NotFound));
        var processor = new WaitManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "gone",
            Deleted = true,
            Interval = 1,
            Timeout = 1,
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task Wait_UsesSessionCancellationTokenDuringPollingDelay()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        var pipeline = new FakeHttpPipeline((_, _) => Json(
            """{ "name": "running", "properties": { "provisioningState": "Running" } }"""));
        var processor = new WaitManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "running",
            Updated = true,
            Interval = 30,
            Timeout = 60,
        };
        Init(
            processor,
            pipeline,
            new FakeOutputHandler(),
            new FakeEventPublisher { Token = cancellation.Token });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessAsync());
        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task Wait_CapsPollingDelayAtRemainingTimeout()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json(
            """{ "name": "running", "properties": { "provisioningState": "Running" } }"""));
        var processor = new WaitManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "running",
            Updated = true,
            Interval = 30,
            Timeout = 1,
        };
        Init(processor, pipeline, new FakeOutputHandler());
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() => processor.ProcessAsync());

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Equal(1, pipeline.SendCount);
    }

    private static HttpResponseMessage WithLocation(HttpResponseMessage response, string location)
    {
        response.Headers.Location = new Uri(location);
        return response;
    }
}
