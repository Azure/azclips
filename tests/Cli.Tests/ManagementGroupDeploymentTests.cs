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
            Assert.Equal(command != "deployment mg delete", metadata.Parameters.ContainsKey("no-prompt"));
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



    private const string EmptyTemplate = """{ "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#", "contentVersion": "1.0.0.0", "resources": [] }""";

    private static readonly Dictionary<string, object> s_emptyTemplateObject = new() { ["resources"] = Array.Empty<object>() };

    private static void UseClient(ClientType clientType)
    {
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.Initialize(new ClientMetadata(clientType, "00000000-0000-0000-0000-000000000000"));
    }

    private static string TempFile(string content, string extension = ".json")
    {
        string path = Path.Combine(Path.GetTempPath(), $"mg-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, content);
        return path;
    }

    private sealed record Request(HttpMethod Method, string Uri, string? Body);

    // Routes fake ARM calls by URL: validate, what-if, deployment PUT/GET and the operations list.
    private static FakeHttpPipeline Arm(
        List<Request> requests,
        string? validate = null,
        string? whatIf = null,
        string? deployment = null,
        string? operations = null)
    {
        return new FakeHttpPipeline((request, _) =>
        {
            string uri = request.RequestUri!.ToString();
            requests.Add(new Request(request.Method, uri, request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()));
            if (uri.Contains("/validate?", StringComparison.Ordinal))
            {
                return Json(validate ?? """{ "properties": { "provisioningState": "Succeeded" } }""");
            }
            if (uri.Contains("/whatIf?", StringComparison.Ordinal))
            {
                return Json(whatIf ?? """{ "status": "Succeeded", "properties": { "changes": [] } }""");
            }
            if (uri.Contains("/operations?", StringComparison.Ordinal))
            {
                return Json(operations ?? """{ "value": [] }""");
            }
            return Json(deployment ?? """{ "id": "/providers/Microsoft.Management/managementGroups/contoso/providers/Microsoft.Resources/deployments/mg-test", "name": "mg-test", "location": "eastus", "properties": { "provisioningState": "Succeeded", "mode": "Incremental" } }""");
        });
    }

    private static JsonElement Body(List<Request> requests, HttpMethod method, string uriPart) =>
        JsonDocument.Parse(requests.First(r => r.Method == method && r.Uri.Contains(uriPart, StringComparison.Ordinal)).Body!).RootElement;

    [Fact]
    public async Task PsNew_ValidatesThenCreatesWithScopedBody()
    {
        UseClient(ClientType.PowerShell);
        List<Request> requests = [];
        var output = new FakeOutputHandler();
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            TemplateObject = s_emptyTemplateObject,
        };
        Init(processor, Arm(requests), output);

        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.EndsWith("/deployments/mg-test/validate?api-version=2025-04-01", requests[0].Uri);
        Assert.Equal(HttpMethod.Put, requests[1].Method);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Management/managementGroups/contoso/providers/Microsoft.Resources/deployments/mg-test?api-version=2025-04-01",
            requests[1].Uri);
        JsonElement body = Body(requests, HttpMethod.Put, "/deployments/mg-test?");
        Assert.Equal("eastus", body.GetProperty("location").GetString());
        Assert.Equal("Incremental", body.GetProperty("properties").GetProperty("mode").GetString());
        // Legacy lists the deployment operations for progress and failed-operation errors.
        Assert.Contains(requests, r => r.Uri.Contains("/deployments/mg-test/operations?", StringComparison.Ordinal));

        PSDeployment deployment = Assert.IsType<PSDeployment>(Assert.Single(output.WrittenValues));
        Assert.Equal("mg-test", deployment.DeploymentName);
        Assert.Equal("contoso", deployment.ManagementGroupId);
        Assert.Equal("Succeeded", deployment.ProvisioningState);
    }

    [Fact]
    public async Task PsNew_ValidationErrorStopsBeforeCreate()
    {
        UseClient(ClientType.PowerShell);
        List<Request> requests = [];
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            TemplateObject = s_emptyTemplateObject,
        };
        Init(processor, Arm(requests, validate: """{ "error": { "code": "InvalidTemplate", "message": "bad" } }"""), new FakeOutputHandler());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());

        Assert.Equal("The deployment validation failed", error.Message);
        Assert.DoesNotContain(requests, r => r.Method == HttpMethod.Put);
    }

    [Theory]
    [InlineData("RequestContent", "RequestContent")]
    [InlineData("ResponseContent", "ResponseContent")]
    [InlineData("All", "RequestContent,ResponseContent")]
    [InlineData("None", null)]
    public async Task PsNew_MapsDeploymentDebugLogLevel(string input, string? expected)
    {
        UseClient(ClientType.PowerShell);
        List<Request> requests = [];
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            TemplateObject = s_emptyTemplateObject,
            DeploymentDebugLogLevel = input,
        };
        Init(processor, Arm(requests), new FakeOutputHandler());

        await processor.ProcessAsync();

        JsonElement properties = Body(requests, HttpMethod.Put, "/deployments/mg-test?").GetProperty("properties");
        Assert.Equal(
            expected,
            properties.TryGetProperty("debugSetting", out JsonElement debug) ? debug.GetProperty("detailLevel").GetString() : null);
    }

    [Fact]
    public async Task PsNew_WhatIfOutputsResultWithoutCreatingAndUsesIncrementalMode()
    {
        UseClient(ClientType.PowerShell);
        List<Request> requests = [];
        var output = new FakeOutputHandler();
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            Mode = "Complete",
            WhatIf = true,
            WhatIfExcludeChangeType = ["NoChange"],
            TemplateObject = s_emptyTemplateObject,
        };
        Init(
            processor,
            Arm(requests, whatIf: """{ "status": "Succeeded", "properties": { "changes": [ { "changeType": "NoChange" }, { "changeType": "Create" } ] } }"""),
            output);

        await processor.ProcessAsync();

        Assert.Single(requests);
        Assert.Equal("Incremental", Body(requests, HttpMethod.Post, "/whatIf?").GetProperty("properties").GetProperty("mode").GetString());
        WhatIfOperationResult result = Assert.IsType<WhatIfOperationResult>(Assert.Single(output.WrittenValues));
        Assert.Equal(["Create"], result.Properties!.Changes!.Select(c => c.ChangeType));
        Assert.True(NewManagementGroupDeploymentProcessor_Orchestrator.HasChanges(result));
    }

    [Fact]
    public async Task PsNew_WhatIfFailedStatusWithoutErrorIsStillOutput()
    {
        UseClient(ClientType.PowerShell);
        List<Request> requests = [];
        var output = new FakeOutputHandler();
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "whatif",
            Location = "eastus",
            WhatIf = true,
            TemplateObject = s_emptyTemplateObject,
        };
        Init(processor, Arm(requests, whatIf: """{ "status": "Failed", "properties": { "changes": [] } }"""), output);

        await processor.ProcessAsync();

        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task PsNew_RejectsUnknownWhatIfExcludeChangeType()
    {
        UseClient(ClientType.PowerShell);
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            WhatIf = true,
            WhatIfExcludeChangeType = ["Bogus"],
            TemplateObject = s_emptyTemplateObject,
        };
        Init(processor, Arm([]), new FakeOutputHandler());

        ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());

        Assert.Contains("'Bogus' is not allowed", error.Message);
    }

    [Fact]
    public void HasChanges_IgnoresNoChangeAndIgnore()
    {
        WhatIfOperationResult result = JsonSerializer.Deserialize<WhatIfOperationResult>(
            """{ "properties": { "changes": [ { "changeType": "NoChange" }, { "changeType": "Ignore" } ] } }""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.False(NewManagementGroupDeploymentProcessor_Orchestrator.HasChanges(result));
    }

    [Theory]
    [InlineData("azuredeploy.json", null, "azuredeploy")]
    [InlineData(null, "https://example.com/templates/linked.json", "linked")]
    public async Task PsNew_DefaultNameIsTemplateBaseName(string? fileName, string? templateUri, string expected)
    {
        UseClient(ClientType.PowerShell);
        string? directory = null;
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Location = "eastus",
            WhatIf = true,
            TemplateUri = templateUri,
        };
        if (fileName is not null)
        {
            directory = Directory.CreateTempSubdirectory().FullName;
            processor.TemplateFile = Path.Combine(directory, fileName);
            File.WriteAllText(processor.TemplateFile, EmptyTemplate);
        }
        Init(processor, Arm([]), new FakeOutputHandler());
        try
        {
            await processor.ProcessAsync();
        }
        finally
        {
            if (directory is not null)
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        Assert.Equal(expected, processor.Name);
    }

    [Fact]
    public async Task PsNew_DefaultNameIsNewGuidForInlineTemplate()
    {
        UseClient(ClientType.PowerShell);
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Location = "eastus",
            WhatIf = true,
            TemplateObject = s_emptyTemplateObject,
        };
        Init(processor, Arm([]), new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.True(Guid.TryParse(processor.Name, out _));
    }

    [Fact]
    public async Task PsNew_WrapsTemplateParameterObjectValuesAndKeepsKeyVaultReferences()
    {
        UseClient(ClientType.PowerShell);
        List<Request> requests = [];
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            WhatIf = true,
            TemplateObject = s_emptyTemplateObject,
            ParametersObject = new System.Collections.Hashtable
            {
                ["count"] = 3,
                ["secret"] = new Dictionary<string, object> { ["reference"] = new Dictionary<string, object> { ["secretName"] = "s" } },
            },
        };
        Init(processor, Arm(requests), new FakeOutputHandler());

        await processor.ProcessAsync();

        JsonElement parameters = Body(requests, HttpMethod.Post, "/whatIf?").GetProperty("properties").GetProperty("parameters");
        Assert.Equal(3, parameters.GetProperty("count").GetProperty("value").GetInt32());
        Assert.Equal("s", parameters.GetProperty("secret").GetProperty("reference").GetProperty("secretName").GetString());
    }

    [Fact]
    public async Task PsNew_ReadsParameterFileWithComments()
    {
        UseClient(ClientType.PowerShell);
        List<Request> requests = [];
        string parametersFile = TempFile(
            """
            {
              // legacy accepts comments
              "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
              "parameters": { "prefix": { "value": "abc" } }
            }
            """);
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            WhatIf = true,
            TemplateObject = s_emptyTemplateObject,
            ParametersFile = parametersFile,
        };
        Init(processor, Arm(requests), new FakeOutputHandler());
        try
        {
            await processor.ProcessAsync();
        }
        finally
        {
            File.Delete(parametersFile);
        }

        JsonElement parameters = Body(requests, HttpMethod.Post, "/whatIf?").GetProperty("properties").GetProperty("parameters");
        Assert.Equal("abc", parameters.GetProperty("prefix").GetProperty("value").GetString());
    }

    [Fact]
    public async Task PsNew_InlinesLocalTemplateFile()
    {
        UseClient(ClientType.PowerShell);
        List<Request> requests = [];
        string templateFile = TempFile(EmptyTemplate);
        var processor = new NewManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            WhatIf = true,
            TemplateFile = templateFile,
        };
        Init(processor, Arm(requests), new FakeOutputHandler());
        try
        {
            await processor.ProcessAsync();
        }
        finally
        {
            File.Delete(templateFile);
        }

        JsonElement properties = Body(requests, HttpMethod.Post, "/whatIf?").GetProperty("properties");
        Assert.Equal(JsonValueKind.Array, properties.GetProperty("template").GetProperty("resources").ValueKind);
        Assert.False(properties.TryGetProperty("templateLink", out _));
    }

    [Theory]
    [InlineData("managementGroupDeploymentTemplate.json", null)]
    [InlineData("deploymentTemplate.json", "resourceGroup")]
    [InlineData("tenantDeploymentTemplate.json", "tenant")]
    [InlineData(null, "None")]
    public void ValidateBicepTargetScope_UsesLegacyMessage(string? schemaFile, string? actualScope)
    {
        string schema = schemaFile is null ? string.Empty : $"\"$schema\": \"https://schema.management.azure.com/schemas/2019-08-01/{schemaFile}#\", ";
        using JsonDocument compiled = JsonDocument.Parse($$"""{ {{schema}}"resources": [] }""");
        MethodInfo method = typeof(ManagementGroupDeploymentOrchestratorBase<ScopedDeployment, DeploymentExtended>)
            .GetMethod("ValidateBicepTargetScope", BindingFlags.Static | BindingFlags.NonPublic)!;

        if (actualScope is null)
        {
            method.Invoke(null, [compiled.RootElement]);
            return;
        }

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [compiled.RootElement]));
        ArgumentException error = Assert.IsType<ArgumentException>(invocation.InnerException);
        Assert.Equal($"The target scope \"{actualScope}\" does not match the deployment scope \"managementGroup\".", error.Message);
    }

    [Fact]
    public async Task PsTest_WritesValidationErrorAsOutput()
    {
        UseClient(ClientType.PowerShell);
        List<Request> requests = [];
        var output = new FakeOutputHandler();
        var processor = new TestManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "invalid",
            Location = "eastus",
            TemplateObject = s_emptyTemplateObject,
        };
        Init(processor, Arm(requests, validate: """{ "error": { "code": "InvalidTemplate", "message": "The template is invalid." } }"""), output);

        await processor.ProcessAsync();

        List<ErrorResponse> errors = Assert.IsType<List<ErrorResponse>>(Assert.Single(output.WrittenValues));
        Assert.Equal("InvalidTemplate", Assert.Single(errors).Code);
    }

    [Fact]
    public async Task PsTest_SendsSingleValidateRequestAndOutputsNothingOnSuccess()
    {
        UseClient(ClientType.PowerShell);
        List<Request> requests = [];
        var output = new FakeOutputHandler();
        var processor = new TestManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "validated",
            Location = "eastus",
            TemplateObject = s_emptyTemplateObject,
        };
        Init(processor, Arm(requests), output);

        await processor.ProcessAsync();

        Request request = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/validate?api-version=2025-04-01", request.Uri);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task CliCreate_ForwardsModeAndOutputsDeployment()
    {
        UseClient(ClientType.CLI);
        List<Request> requests = [];
        var output = new FakeOutputHandler();
        string templateFile = TempFile(EmptyTemplate);
        var processor = new CreateManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            Mode = "Complete",
            TemplateFile = templateFile,
        };
        Init(processor, Arm(requests), output);
        try
        {
            await processor.ProcessAsync();
        }
        finally
        {
            File.Delete(templateFile);
        }

        Assert.Equal("Complete", Body(requests, HttpMethod.Put, "/deployments/mg-test?").GetProperty("properties").GetProperty("mode").GetString());
        DeploymentExtended result = Assert.IsType<DeploymentExtended>(Assert.Single(output.WrittenValues));
        Assert.Equal("mg-test", result.Name);
    }

    [Fact]
    public async Task CliCreate_DefaultNameIsTemplateFileBaseName()
    {
        UseClient(ClientType.CLI);
        string directory = Directory.CreateTempSubdirectory().FullName;
        string templateFile = Path.Combine(directory, "mgtemplate.json");
        File.WriteAllText(templateFile, EmptyTemplate);
        var processor = new CreateManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Location = "eastus",
            WhatIf = true,
            TemplateFile = templateFile,
        };
        Init(processor, Arm([]), new FakeOutputHandler());
        try
        {
            await processor.ProcessAsync();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }

        Assert.Equal("mgtemplate", processor.Name);
    }

    [Fact]
    public async Task CliCreate_ConfirmWithWhatIfAndProceedIfNoChange_SkipsPromptAndCreates()
    {
        UseClient(ClientType.CLI);
        List<Request> requests = [];
        string templateFile = TempFile(EmptyTemplate);
        var processor = new CreateManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            ConfirmWithWhatIf = true,
            ProceedIfNoChange = true,
            TemplateFile = templateFile,
        };
        Init(
            processor,
            Arm(requests, whatIf: """{ "status": "Succeeded", "properties": { "changes": [ { "changeType": "NoChange" } ] } }"""),
            new FakeOutputHandler());
        try
        {
            await processor.ProcessAsync();
        }
        finally
        {
            File.Delete(templateFile);
        }

        Assert.Equal(["whatIf", "validate", "put"], requests
            .Where(r => r.Method != HttpMethod.Get)
            .Select(r => r.Uri.Contains("/whatIf?", StringComparison.Ordinal) ? "whatIf" : r.Uri.Contains("/validate?", StringComparison.Ordinal) ? "validate" : "put"));
    }

    [Fact]
    public async Task CliCreate_FailedDeploymentThrowsArmError()
    {
        UseClient(ClientType.CLI);
        string templateFile = TempFile(EmptyTemplate);
        var processor = new CreateManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            TemplateFile = templateFile,
        };
        Init(
            processor,
            Arm([], deployment: """{ "name": "mg-test", "properties": { "provisioningState": "Failed", "error": { "code": "DeploymentFailed", "message": "boom" } } }"""),
            new FakeOutputHandler());
        try
        {
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
            Assert.StartsWith("(DeploymentFailed) boom", error.Message);
        }
        finally
        {
            File.Delete(templateFile);
        }
    }

    [Fact]
    public async Task CliValidate_AlwaysUsesIncrementalModeAndTypesKeyValueParameters()
    {
        UseClient(ClientType.CLI);
        List<Request> requests = [];
        string templateFile = TempFile(
            """
            {
              "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
              "parameters": {
                "count": { "type": "int" },
                "flag": { "type": "bool" },
                "name": { "type": "string" }
              },
              "resources": []
            }
            """);
        var processor = new ValidateManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            Mode = "Complete",
            TemplateFile = templateFile,
            CliParameters = ["count=3", "flag=true", "name=01234"],
        };
        Init(processor, Arm(requests), new FakeOutputHandler());
        try
        {
            await processor.ProcessAsync();
        }
        finally
        {
            File.Delete(templateFile);
        }

        JsonElement properties = Body(requests, HttpMethod.Post, "/validate?").GetProperty("properties");
        Assert.Equal("Incremental", properties.GetProperty("mode").GetString());
        JsonElement parameters = properties.GetProperty("parameters");
        Assert.Equal(3, parameters.GetProperty("count").GetProperty("value").GetInt32());
        Assert.True(parameters.GetProperty("flag").GetProperty("value").GetBoolean());
        Assert.Equal("01234", parameters.GetProperty("name").GetProperty("value").GetString());
    }

    [Fact]
    public async Task CliValidate_ThrowsValidationError()
    {
        UseClient(ClientType.CLI);
        string templateFile = TempFile(EmptyTemplate);
        var processor = new ValidateManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "invalid",
            Location = "eastus",
            TemplateFile = templateFile,
        };
        Init(processor, Arm([], validate: """{ "error": { "code": "InvalidTemplate", "message": "The template is invalid." } }"""), new FakeOutputHandler());
        try
        {
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());
            Assert.Contains("InvalidTemplate", error.Message);
        }
        finally
        {
            File.Delete(templateFile);
        }
    }

    [Fact]
    public async Task CliCommands_RequireATemplateSource()
    {
        UseClient(ClientType.CLI);
        var processor = new ValidateManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
        };
        Init(processor, Arm([]), new FakeOutputHandler());

        ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());

        Assert.Equal("Please enter one of the following: template file, template spec, template url, or Bicep parameters file.", error.Message);
    }

    [Fact]
    public async Task CliWhatIf_AlwaysUsesIncrementalMode()
    {
        UseClient(ClientType.CLI);
        List<Request> requests = [];
        string templateFile = TempFile(EmptyTemplate);
        var output = new FakeOutputHandler();
        var processor = new WhatIfManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "mg-test",
            Location = "eastus",
            Mode = "Complete",
            TemplateFile = templateFile,
        };
        Init(processor, Arm(requests, whatIf: """{ "status": "Failed", "properties": { "changes": [] } }"""), output);
        try
        {
            await processor.ProcessAsync();
        }
        finally
        {
            File.Delete(templateFile);
        }

        Assert.Equal("Incremental", Body(requests, HttpMethod.Post, "/whatIf?").GetProperty("properties").GetProperty("mode").GetString());
        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task CliWhatIf_FollowsGeneratedFinalLocation()
    {
        UseClient(ClientType.CLI);
        List<(HttpMethod Method, Uri Uri)> requests = [];
        const string location = "https://management.azure.com/providers/Microsoft.Resources/operations/whatif-1";
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!));
            return requests.Count == 1
                ? WithLocation(Json("""{ "status": "Accepted" }""", HttpStatusCode.Accepted), location)
                : Json("""{ "status": "Succeeded", "properties": { "changes": [] } }""");
        });
        string templateFile = TempFile(EmptyTemplate);
        var processor = new WhatIfManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "whatif",
            Location = "eastus",
            TemplateFile = templateFile,
        };
        Init(processor, pipeline, new FakeOutputHandler());
        try
        {
            await processor.ProcessAsync();
        }
        finally
        {
            File.Delete(templateFile);
        }

        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.EndsWith("/whatIf?api-version=2025-04-01", requests[0].Uri.ToString());
        Assert.All(requests.Skip(1), r => Assert.Equal(location, r.Uri.ToString()));
    }

    [Fact]
    public void PSDeployment_From_MapsLegacyShapeAndTables()
    {
        const string raw = """
            {
              "id": "/providers/Microsoft.Management/managementGroups/contoso/providers/Microsoft.Resources/deployments/d1",
              "name": "d1",
              "location": "eastus",
              "tags": { "env": "test" },
              "properties": {
                "provisioningState": "Succeeded",
                "mode": "Incremental",
                "correlationId": "c1",
                "timestamp": "2024-01-02T03:04:05Z",
                "templateLink": { "uri": "https://example.com/t.json", "contentVersion": "1.0.0.0" },
                "debugSetting": { "detailLevel": "RequestContent" },
                "parameters": { "prefix": { "type": "String", "value": "abc" } },
                "outputs": { "count": { "type": "Int", "value": 3 } },
                "onErrorDeployment": { "type": "LastSuccessful", "deploymentName": "d0" }
              }
            }
            """;

        PSDeployment deployment = PSDeployment.From(JsonSerializer.Deserialize<DeploymentExtended>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))!, "contoso");

        Assert.Equal("d1", deployment.DeploymentName);
        Assert.Equal("contoso", deployment.ManagementGroupId);
        Assert.Equal("c1", deployment.CorrelationId);
        Assert.Equal(DeploymentMode.Incremental, deployment.Mode);
        Assert.Equal("RequestContent", deployment.DeploymentDebugLogLevel);
        Assert.Equal("test", deployment.Tags!["env"]);
        Assert.Equal("https://example.com/t.json", deployment.TemplateLink?.Uri);
        Assert.Contains("https://example.com/t.json", deployment.TemplateLinkString);
        Assert.Equal("abc", deployment.Parameters!["prefix"].Value);
        Assert.Equal(3L, deployment.Outputs!["count"].Value);
        Assert.Contains("prefix", deployment.ParametersString);
        Assert.Contains("\"abc\"", deployment.ParametersString);
        Assert.Contains("Int", deployment.OutputsString);
        Assert.Equal("d0", deployment.OnErrorDeployment?.DeploymentName);
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
            GroupId = "contoso",
            DeploymentName = "mg-test",
            Custom = "properties.outputs.result.value=='ready'",
            Interval = 1,
            Timeout = 5,
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task List_UnwrapsManagementGroupDeploymentPage()
    {
        UseClient(ClientType.CLI);
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
        UseClient(ClientType.PowerShell);
        List<(HttpMethod Method, Uri Uri)> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!));
            return requests.Count switch
            {
                1 => Json("""{ "name": "active", "properties": { "provisioningState": "Running" } }"""),
                2 => Json("{}"),
                _ => throw new InvalidOperationException("Unexpected request."),
            };
        });
        var processor = new CancelManagementGroupDeploymentProcessor_Orchestrator
        {
            GroupId = "contoso",
            DeploymentName = "active",
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
        UseClient(ClientType.PowerShell);
        var pipeline = new FakeHttpPipeline((_, _) =>
            Json("""{ "name": "done", "properties": { "provisioningState": "Succeeded" } }"""));
        var processor = new CancelManagementGroupDeploymentProcessor_Orchestrator
        {
            GroupId = "contoso",
            DeploymentName = "done",
        };
        Init(processor, pipeline, new FakeOutputHandler());

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());

        Assert.Equal("There is no running deployment called 'done' at management group 'contoso'.", exception.Message);
        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task Cancel_Cli_SkipsPowerShellPrecheck()
    {
        UseClient(ClientType.CLI);
        List<HttpMethod> methods = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            methods.Add(request.Method);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var processor = new CancelManagementGroupDeploymentProcessor_Orchestrator
        {
            GroupId = "contoso",
            DeploymentName = "done",
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal([HttpMethod.Post], methods);
    }

    [Fact]
    public async Task Remove_ThrowsWhenHeadReturnsNotFound()
    {
        UseClient(ClientType.PowerShell);
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            Assert.Equal(HttpMethod.Head, request.Method);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var processor = new DeleteManagementGroupDeploymentProcessor_Orchestrator
        {
            GroupId = "contoso",
            DeploymentName = "missing",
        };
        Init(processor, pipeline, new FakeOutputHandler());

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());

        Assert.Equal("Deployment 'missing' doesn't exist at management group 'contoso'.", exception.Message);
        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task Delete_Cli_SkipsPowerShellHeadCheck()
    {
        UseClient(ClientType.CLI);
        List<HttpMethod> methods = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            methods.Add(request.Method);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var processor = new DeleteManagementGroupDeploymentProcessor_Orchestrator
        {
            GroupId = "contoso",
            DeploymentName = "mg-test",
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal([HttpMethod.Delete], methods);
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
            GroupId = "contoso",
            DeploymentName = "gone",
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
            GroupId = "contoso",
            DeploymentName = "running",
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
            GroupId = "contoso",
            DeploymentName = "running",
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
