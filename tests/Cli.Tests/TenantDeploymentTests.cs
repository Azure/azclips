// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cli.Tests;

/// <summary>
/// Offline behavior of the tenant deployment customization (<c>az deployment tenant *</c> and
/// <c>*-AzTenantDeployment</c>) against the legacy azure-cli / Az.Resources workflows.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class TenantDeploymentTests : IDisposable
{
    private const string Arm = "https://management.azure.com/providers/Microsoft.Resources/deployments";
    private const string ApiVersion = "?api-version=2025-04-01";

    private readonly List<string> _warnings = [];
    private readonly List<string> _verbose = [];
    private readonly string _dir = Directory.CreateTempSubdirectory("tenant-deployment-tests").FullName;

    public TenantDeploymentTests() => UseClient(ClientType.CLI);

    private void UseClient(ClientType clientType)
    {
        var logger = new Mock<ILoggingService>();
        logger.Setup(l => l.Warn(It.IsAny<string>())).Callback<string>(_warnings.Add);
        logger.Setup(l => l.Verbose(It.IsAny<string>())).Callback<string>(_verbose.Add);
        AzclipsServiceProvider.Dispose();
        AzclipsServiceProvider.OverrideRegistration(services => services.AddSingleton(logger.Object));
        AzclipsServiceProvider.Initialize(new ClientMetadata(clientType, "00000000-0000-0000-0000-000000000000"));
    }

    public void Dispose()
    {
        AzclipsServiceProvider.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    // ── Generated processors and command surface ──

    [Theory]
    [InlineData(typeof(CancelAtTenantScopeDeploymentsProcessor), "/deployments/name%2Fwith%3Freserved%23chars/cancel?")]
    [InlineData(typeof(DeleteAtTenantScopeDeploymentsProcessor), "/deployments/name%2Fwith%3Freserved%23chars?")]
    [InlineData(typeof(ExportTemplateAtTenantScopeDeploymentsProcessor), "/deployments/name%2Fwith%3Freserved%23chars/exportTemplate?")]
    [InlineData(typeof(GetAtTenantScopeDeploymentsProcessor), "/deployments/name%2Fwith%3Freserved%23chars?")]
    public void DeploymentName_IsEscapedAsSinglePathSegment(Type processorType, string expected)
    {
        var processor = (IProcessor)Activator.CreateInstance(processorType)!;
        processorType.GetProperty("DeploymentName")!.SetValue(processor, "name/with?reserved#chars");

        Assert.Contains(expected, RequestUri(processor));
    }

    [Fact]
    public void List_SendsFilterOnlyWhenSet()
    {
        Assert.Equal(Arm + ApiVersion, RequestUri(new ListTenantDeploymentProcessor_Orchestrator()));
        Assert.Equal(
            Arm + ApiVersion + "&$filter=provisioningState%20eq%20%27Succeeded%27%20%26%20name%20eq%20%27a%2Fb%27",
            RequestUri(new ListTenantDeploymentProcessor_Orchestrator { _filter = "provisioningState eq 'Succeeded' & name eq 'a/b'" }));
    }

    [Fact]
    public void ExtensionIndex_ExposesLegacyTenantCommands()
    {
        var index = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex();

        Assert.Equal(
            [
                "deployment tenant cancel",
                "deployment tenant create",
                "deployment tenant delete",
                "deployment tenant export",
                "deployment tenant list",
                "deployment tenant show",
                "deployment tenant validate",
                "deployment tenant wait",
                "deployment tenant what-if",
            ],
            index.GetCommandKeys(ClientType.CLI).Where(key => key.StartsWith("deployment tenant ", StringComparison.Ordinal)).Order().ToArray());
        Assert.Equal(
            [
                "Get-AzTenantDeployment_Get",
                "Get-AzTenantDeployment_List",
                "New-AzTenantDeployment",
                "Remove-AzTenantDeployment",
                "Stop-AzTenantDeployment",
                "Test-AzTenantDeployment",
            ],
            index.GetCommandKeys(ClientType.PowerShell).Where(key => key.Contains("TenantDeployment", StringComparison.Ordinal)).Order().ToArray());

        // --no-wait is intentionally not migrated.
        foreach (string command in index.GetCommandKeys(ClientType.CLI).Where(key => key.StartsWith("deployment tenant ", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("no-wait", index.GetCommandMetadata(command, ClientType.CLI).Parameters.Keys);
        }

        CommandMetadata create = index.GetCommandMetadata("deployment tenant create", ClientType.CLI);
        Assert.Equal("CliParameters", create.Parameters["parameters"].Name);
        Assert.Equal("NoPrompt", create.Parameters["no-prompt"].Name);
        Assert.Equal("confirm-with-what-if", create.ParameterAliases["c"]);
        Assert.Equal("what-if-exclude-change-types", create.ParameterAliases["x"]);
        Assert.Equal("NoPrettyPrint", index.GetCommandMetadata("deployment tenant what-if", ClientType.CLI).Parameters["no-pretty-print"].Name);

        CommandMetadata newPs = index.GetCommandMetadata("New-AzTenantDeployment", ClientType.PowerShell);
        Assert.Equal("ParametersFile", newPs.Parameters["TemplateParameterFile"].Name);
        Assert.Equal("ParametersObject", newPs.Parameters["TemplateParameterObject"].Name);
        Assert.Equal("Tags", newPs.Parameters["Tag"].Name);
        Assert.Equal("WhatIfExcludeChangeTypes", newPs.Parameters["WhatIfExcludeChangeType"].Name);
        Assert.Equal("SuppressDiagnostics", index.GetCommandMetadata("Test-AzTenantDeployment", ClientType.PowerShell).Parameters["SuppressDiagnostics"].Name);
    }

    // ── az deployment tenant create ──

    [Fact]
    public async Task CliCreate_ValidatesThenPutsIncrementalDeploymentAndWritesRawResult()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var output = new FakeOutputHandler();
        var processor = new CreateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline(requests), output);

        await processor.ProcessAsync();

        Assert.Equal((HttpMethod.Post, $"{Arm}/tenant-test/validate{ApiVersion}"), (requests[0].Method, requests[0].Uri));
        Assert.Equal((HttpMethod.Put, $"{Arm}/tenant-test{ApiVersion}"), (requests[1].Method, requests[1].Uri));
        JsonElement body = JsonDocument.Parse(requests[1].Body!).RootElement;
        Assert.Equal("eastus", body.GetProperty("location").GetString());
        Assert.Equal("Incremental", body.GetProperty("properties").GetProperty("mode").GetString());
        Assert.False(body.GetProperty("properties").TryGetProperty("validationLevel", out _));
        Assert.Equal("1.0.0.0", body.GetProperty("properties").GetProperty("template").GetProperty("contentVersion").GetString());

        // The raw ARM body is written, so fields the generated model drops are kept.
        JsonElement result = Assert.IsType<JsonElement>(Assert.Single(output.WrittenValues));
        Assert.Equal("tenant-test", result.GetProperty("name").GetString());
        Assert.Equal("extra", result.GetProperty("properties").GetProperty("unmodeled").GetString());
    }

    [Fact]
    public async Task CliCreate_DefaultsNameToTemplateFileName()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var processor = new CreateTenantDeploymentProcessor_Orchestrator { Location = "eastus", TemplateFile = TemplateFile("azuredeploy.json") };
        Init(processor, Pipeline(requests), new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal("azuredeploy", processor.Name);
        Assert.Equal($"{Arm}/azuredeploy{ApiVersion}", requests[1].Uri);
    }

    [Theory]
    [InlineData(true, false, "incorrect usage: Choose only one of --template-file FILE | --template-uri URI | --template-spec ID to pass in")]
    [InlineData(false, false, "Please enter one of the following: template file, template spec, template url, or Bicep parameters file.")]
    [InlineData(false, true, "please provide --template-uri if --query-string is specified")]
    public async Task CliCreate_RejectsInvalidTemplateSourcesBeforeSending(bool fileAndUri, bool queryString, string expected)
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var processor = new CreateTenantDeploymentProcessor_Orchestrator
        {
            Location = "eastus",
            TemplateFile = fileAndUri || queryString ? TemplateFile() : null,
            TemplateUri = fileAndUri ? "https://example.com/azuredeploy.json" : null,
            CliQueryString = queryString ? "sig=abc" : null,
        };
        Init(processor, Pipeline(requests), new FakeOutputHandler());

        ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(processor.ProcessAsync);

        Assert.Equal(expected, error.Message);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task CliCreate_MergesParametersInOrderAndTypesKeyValueByTemplate()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var processor = new CreateTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateFile = TemplateFile(parameters: """{ "first": { "type": "string" }, "shared": { "type": "string" }, "count": { "type": "int" } }"""),
            CliParameters =
            [
                """{ "first": { "value": "one" }, "shared": { "value": "one" } }""",
                """{ "shared": { "value": "two" } }""",
                "count=3",
            ],
        };
        Init(processor, Pipeline(requests), new FakeOutputHandler());

        await processor.ProcessAsync();

        JsonElement parameters = JsonDocument.Parse(requests[1].Body!).RootElement.GetProperty("properties").GetProperty("parameters");
        Assert.Equal("one", parameters.GetProperty("first").GetProperty("value").GetString());
        Assert.Equal("two", parameters.GetProperty("shared").GetProperty("value").GetString());
        Assert.Equal(3, parameters.GetProperty("count").GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task CliCreate_RejectsKeyValueForUnknownTemplateParameter()
    {
        var processor = new CreateTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateFile = TemplateFile(parameters: """{ "first": { "type": "string" } }"""),
            CliParameters = ["nope=1"],
        };
        Init(processor, Pipeline([]), new FakeOutputHandler());

        ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(processor.ProcessAsync);

        Assert.Equal("unrecognized template parameter 'nope'. Allowed parameters: first", error.Message);
    }

    [Fact]
    public async Task CliCreate_NoPromptWarnsAboutMissingParametersAndDeprecatedJsonFlag()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var processor = new CreateTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateFile = TemplateFile(parameters: """{ "required": { "type": "string" }, "optional": { "type": "string", "defaultValue": "x" } }"""),
            NoPrompt = true,
            HandleExtendedJsonFormat = true,
        };
        Init(processor, Pipeline(requests), new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(
            [
                "Argument '--handle-extended-json-format/-j' has been deprecated and will be removed in a future release.",
                "Missing input parameters: required ",
            ],
            _warnings);
        Assert.Equal(HttpMethod.Put, requests[1].Method);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\":\"InvalidTemplate\",\"message\":\"The template is invalid.\"}\n\nInner Errors: \n{\"code\":\"InvalidParameter\",\"message\":\"Bad parameter.\"}")]
    [InlineData(HttpStatusCode.OK, "InvalidTemplate - The template is invalid.\nInvalidParameter - Bad parameter.")]
    public async Task CliCreate_ValidationErrorStopsDeploymentWithLegacyMessage(HttpStatusCode status, string expected)
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var processor = new CreateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline(requests, r => r.RequestUri!.AbsolutePath.EndsWith("/validate") ? Json(ValidationError, status) : null), new FakeOutputHandler());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);

        Assert.Equal(expected, error.Message);
        Assert.Single(requests);
    }

    [Fact]
    public async Task CliCreate_FailedDeploymentRaisesRawResponseBody()
    {
        var processor = new CreateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline([], r => r.Method != HttpMethod.Post ? Json(Deployment("Failed")) : null), new FakeOutputHandler());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);

        Assert.Equal(Deployment("Failed"), error.Message);
    }

    [Fact]
    public async Task CliCreate_PollingFailureRaisesRawPollingBody()
    {
        const string failedStatus = """{ "status": "Failed", "error": { "code": "DeploymentFailed", "message": "At least one resource deployment operation failed." } }""";
        var processor = new CreateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline([], r =>
            r.Method == HttpMethod.Put ? WithHeaders(Json("""{ "status": "Accepted" }""", HttpStatusCode.Created), ("Azure-AsyncOperation", "https://management.azure.com/providers/Microsoft.Resources/operations/create-async"))
            : r.RequestUri!.AbsolutePath.EndsWith("/create-async") ? Json(failedStatus)
            : null), new FakeOutputHandler());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);

        Assert.Equal(failedStatus, error.Message);
    }

    [Fact]
    public async Task CliCreate_HttpFailurePublishesRawResponseBody()
    {
        const string conflict = """{ "error": { "code": "Conflict", "message": "The resource is busy." } }""";
        var publisher = new FakeEventPublisher();
        var processor = new CreateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline([], r => r.Method == HttpMethod.Put ? Json(conflict, HttpStatusCode.Conflict) : null), new FakeOutputHandler(), publisher);

        await Assert.ThrowsAsync<HttpRequestException>(processor.ProcessAsync);

        Assert.Equal(conflict, Assert.Single(FailureMessages(publisher)));
    }

    [Fact]
    public async Task CliCreate_PrefersAsyncOperationThenReadsFinalDeployment()
    {
        List<string> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add(request.RequestUri!.ToString());
            return requests.Count switch
            {
                1 => Json("{}"),
                2 => WithHeaders(
                    Json("""{ "status": "Accepted" }""", HttpStatusCode.Accepted),
                    ("Azure-AsyncOperation", "https://management.azure.com/providers/Microsoft.Resources/operations/create-async"),
                    ("Location", "https://management.azure.com/providers/Microsoft.Resources/operations/create-location")),
                3 => Json("""{ "status": "Succeeded" }"""),
                4 => Json(Deployment("Succeeded")),
                _ => throw new InvalidOperationException("Unexpected request."),
            };
        });
        var processor = new CreateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(
            [
                $"{Arm}/tenant-test/validate{ApiVersion}",
                $"{Arm}/tenant-test{ApiVersion}",
                "https://management.azure.com/providers/Microsoft.Resources/operations/create-async",
                "https://management.azure.com/providers/Microsoft.Resources/operations/create-location",
            ],
            requests);
    }

    [Fact]
    public async Task CliCreate_WhatIfWritesFormattedPreviewWithoutDeploying()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var output = new FakeOutputHandler();
        var processor = new CreateTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateFile = TemplateFile(),
            WhatIf = true,
            WhatIfResultFormat = "resourceidonly",
        };
        Init(processor, Pipeline(requests, r => r.RequestUri!.AbsolutePath.EndsWith("/whatIf") ? Json(WhatIfResult) : null), output);

        await processor.ProcessAsync();

        var (method, uri, body) = Assert.Single(requests);
        Assert.Equal((HttpMethod.Post, $"{Arm}/tenant-test/whatIf{ApiVersion}"), (method, uri));
        JsonElement properties = JsonDocument.Parse(body!).RootElement.GetProperty("properties");
        Assert.Equal("Incremental", properties.GetProperty("mode").GetString());
        Assert.Equal("ResourceIdOnly", properties.GetProperty("whatIfSettings").GetProperty("resultFormat").GetString());
        Assert.Contains("Resource changes: 1 to create, 1 no change.", Assert.IsType<string>(Assert.Single(output.WrittenValues)));
    }

    [Theory]
    [InlineData("""{ "status": "Failed", "error": { "code": "InvalidTemplate", "message": "bad" } }""", false)]
    [InlineData("""{ "status": "Succeeded", "properties": { "changes": [ { "resourceId": "/providers/Microsoft.Management/managementGroups/mg1", "changeType": "Create" } ] } }""", true)]
    public void HasChanges_IgnoresNoChangeAndIgnore(string rawResult, bool expected)
    {
        Assert.Equal(expected, CreateTenantDeploymentProcessor_Orchestrator.HasChanges(ToCliResult(rawResult, null)));
        Assert.False(CreateTenantDeploymentProcessor_Orchestrator.HasChanges(ToCliResult(WhatIfResult, ["Create"])));
    }

    // ── New-AzTenantDeployment ──

    [Theory]
    [InlineData("All", "RequestContent,ResponseContent")]
    [InlineData("requestcontent", "RequestContent")]
    [InlineData("None", null)]
    public async Task PsCreate_SendsTagsAndDebugSettingWithoutValidationLevelAndWritesPSDeployment(string level, string? expected)
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var output = new FakeOutputHandler();
        var processor = new NewTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateObject = Template(),
            Tags = new Dictionary<string, string> { ["env"] = "test" },
            DeploymentDebugLogLevel = level,
            ValidationLevel = "Provider",
        };
        Init(processor, Pipeline(requests), output);

        await processor.ProcessAsync();

        JsonElement body = JsonDocument.Parse(requests.First(r => r.Method == HttpMethod.Put).Body!).RootElement;
        Assert.Equal("test", body.GetProperty("tags").GetProperty("env").GetString());
        Assert.False(body.GetProperty("properties").TryGetProperty("validationLevel", out _));
        Assert.Equal(expected, body.GetProperty("properties").TryGetProperty("debugSetting", out JsonElement debug) ? debug.GetProperty("detailLevel").GetString() : null);
        Assert.Equal(expected is null ? 0 : 1, _warnings.Count(w => w.StartsWith("The DeploymentDebug setting has been enabled.", StringComparison.Ordinal)));
        Assert.Contains("Template is valid.", _verbose);

        PSDeployment deployment = Assert.IsType<PSDeployment>(Assert.Single(output.WrittenValues));
        Assert.Equal("tenant-test", deployment.DeploymentName);
        Assert.Equal("Succeeded", deployment.ProvisioningState);
    }

    [Fact]
    public async Task PsCreate_DefaultsNameToGuidForTemplateObject()
    {
        var processor = new NewTenantDeploymentProcessor_Orchestrator { Location = "eastus", TemplateObject = Template() };
        Init(processor, Pipeline([]), new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.True(Guid.TryParse(processor.Name, out _));
    }

    [Fact]
    public async Task PsCreate_ValidationFailureWritesEachErrorThenThrows()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var publisher = new FakeEventPublisher();
        var processor = new NewTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateObject = Template() };
        Init(processor, Pipeline(requests, r => r.RequestUri!.AbsolutePath.EndsWith("/validate") ? Json(ValidationError, HttpStatusCode.BadRequest) : null), new FakeOutputHandler(), publisher);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);

        Assert.Equal("The deployment validation failed", error.Message);
        Assert.Equal(
            [
                "Error: Code=InvalidTemplate; Message=The template is invalid.\r\n",
                "Error: Code=InvalidParameter; Message=Bad parameter.\r\n",
            ],
            FailureMessages(publisher));
        Assert.DoesNotContain(requests, r => r.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task PsCreate_FailedDeploymentReportsOperationErrorsAndStillWritesDeployment()
    {
        var publisher = new FakeEventPublisher();
        var output = new FakeOutputHandler();
        var processor = new NewTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateObject = Template() };
        Init(processor, Pipeline([], r => r.RequestUri!.AbsolutePath.EndsWith("/operations")
            ? Json("""
                {
                  "value": [
                    { "operationId": "op1", "properties": { "provisioningState": "Failed", "statusMessage": { "error": { "code": "Conflict", "message": "The resource is busy." } } } },
                    { "operationId": "op2", "properties": { "provisioningState": "Succeeded" } }
                  ]
                }
                """)
            : r.Method != HttpMethod.Post ? Json(Deployment("Failed")) : null), output, publisher);

        await processor.ProcessAsync();

        string nl = Environment.NewLine;
        Assert.Equal(
            $"The deployment 'tenant-test' failed with error(s). Showing 1 out of 1 error(s).{nl}Status Message: The resource is busy. (Code:Conflict){nl}{nl}CorrelationId: corr-1",
            Assert.Single(FailureMessages(publisher)));
        Assert.Equal("Failed", Assert.IsType<PSDeployment>(Assert.Single(output.WrittenValues)).ProvisioningState);
    }

    [Fact]
    public async Task PsCreate_WhatIfWritesFilteredResultIncludingTemplateError()
    {
        var output = new FakeOutputHandler();
        var processor = new NewTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateObject = Template(),
            WhatIf = true,
            WhatIfExcludeChangeTypes = ["nochange"],
        };
        Init(processor, Pipeline([], r => Json(WhatIfResult.Replace("\"Succeeded\"", "\"Failed\", \"error\": { \"code\": \"InvalidTemplate\", \"message\": \"bad\" }"))), output);

        await processor.ProcessAsync();

        JsonObject result = Assert.IsType<JsonObject>(Assert.Single(output.WrittenValues));
        Assert.Equal("InvalidTemplate", (string?)result["error"]?["code"]);
        Assert.Equal("Create", (string?)Assert.Single(result["changes"]!.AsArray())!["changeType"]);
    }

    // ── az deployment tenant what-if ──

    [Fact]
    public async Task CliWhatIf_PrettyPrintsByDefaultAndWritesFlattenedJsonWithNoPrettyPrint()
    {
        var pretty = new FakeOutputHandler();
        var prettyProcessor = new WhatIfTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(prettyProcessor, Pipeline([], _ => Json(WhatIfResult)), pretty);
        var json = new FakeOutputHandler();
        var jsonProcessor = new WhatIfTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateFile = TemplateFile(),
            NoPrettyPrint = true,
            ExcludeChangeTypes = ["NoChange"],
        };
        Init(jsonProcessor, Pipeline([], _ => Json(WhatIfResult)), json);

        await prettyProcessor.ProcessAsync();
        await jsonProcessor.ProcessAsync();

        Assert.StartsWith("Note: The result may contain false positive predictions (noise).", Assert.IsType<string>(Assert.Single(pretty.WrittenValues)));
        JsonObject result = Assert.IsType<JsonObject>(Assert.Single(json.WrittenValues));
        Assert.Equal(["changes", "diagnostics", "error", "potentialChanges", "status"], result.Select(p => p.Key));
        Assert.Equal("Create", (string?)Assert.Single(result["changes"]!.AsArray())!["changeType"]);
    }

    [Fact]
    public async Task CliWhatIf_TemplateErrorRaisesPreflightMessage()
    {
        var processor = new WhatIfTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline([], _ => Json("""{ "status": "Failed", "error": { "code": "InvalidTemplate", "message": "bad", "details": [ { "code": "Inner", "message": "inner" } ] } }""")), new FakeOutputHandler());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);

        Assert.Equal("InvalidTemplate - bad\nInner - inner", error.Message);
    }

    [Fact]
    public async Task CliWhatIf_PolledTemplateErrorRaisesPreflightMessage()
    {
        var processor = new WhatIfTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline([], r => r.Method == HttpMethod.Post
            ? WithHeaders(Json("", HttpStatusCode.Accepted), ("Location", "https://management.azure.com/providers/Microsoft.Resources/operations/whatif-location"))
            : Json("""{ "status": "Failed", "error": { "code": "InvalidTemplate", "message": "bad" } }""")), new FakeOutputHandler());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);

        Assert.Equal("InvalidTemplate - bad", error.Message);
    }

    [Fact]
    public async Task CliWhatIf_RejectsUnknownExcludeChangeType()
    {
        var processor = new WhatIfTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile(), ExcludeChangeTypes = ["Bogus"] };
        Init(processor, Pipeline([]), new FakeOutputHandler());

        ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(processor.ProcessAsync);

        Assert.Equal("Parameter 'exclude-change-types' value 'Bogus' is not allowed. Allowed values: Create, Delete, Deploy, Ignore, Modify, NoChange, Unsupported", error.Message);
    }

    // ── az deployment tenant validate / Test-AzTenantDeployment ──

    [Fact]
    public async Task CliValidate_WritesRawResult()
    {
        var output = new FakeOutputHandler();
        var processor = new ValidateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline([], _ => Json("""{ "properties": { "provisioningState": "Succeeded", "validatedResources": [ { "id": "r1" } ] } }""")), output);

        await processor.ProcessAsync();

        JsonElement result = Assert.IsType<JsonElement>(Assert.Single(output.WrittenValues));
        Assert.Equal("r1", result.GetProperty("properties").GetProperty("validatedResources")[0].GetProperty("id").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "{\"code\":\"InvalidTemplate\",\"message\":\"The template is invalid.\"}\n\nInner Errors: \n{\"code\":\"InvalidParameter\",\"message\":\"Bad parameter.\"}")]
    [InlineData(HttpStatusCode.OK, "InvalidTemplate - The template is invalid.\nInvalidParameter - Bad parameter.")]
    public async Task CliValidate_RaisesValidationErrors(HttpStatusCode status, string expected)
    {
        var processor = new ValidateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline([], _ => Json(ValidationError, status)), new FakeOutputHandler());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);

        Assert.Equal(expected, error.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.OK)]
    public async Task PsTest_WritesValidationErrorsInsteadOfThrowing(HttpStatusCode status)
    {
        var output = new FakeOutputHandler();
        var processor = new TestTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateObject = Template() };
        Init(processor, Pipeline([], _ => Json(ValidationError, status)), output);

        await processor.ProcessAsync();

        PSResourceManagerError error = Assert.Single(Assert.IsType<List<PSResourceManagerError>>(Assert.Single(output.WrittenValues)));
        Assert.Equal(("InvalidTemplate", "The template is invalid.", null), (error.Code, error.Message, error.Target));
        Assert.Equal("InvalidParameter", Assert.Single(error.Details!).Code);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task PsTest_SuccessWritesNothingAndWarnsDiagnosticsUnlessSuppressed(bool suppress, int warnings)
    {
        var output = new FakeOutputHandler();
        var processor = new TestTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateObject = Template(), SuppressDiagnostics = suppress };
        Init(processor, Pipeline([], _ => Json("""{ "properties": { "diagnostics": [ { "level": "Warning", "code": "W1", "message": "Watch out.", "target": "r1" } ] } }""")), output);

        await processor.ProcessAsync();

        Assert.Empty(output.WrittenValues);
        Assert.Contains("Template is valid.", _verbose);
        Assert.Equal(warnings, _warnings.Count(w => w.Contains("(r1) Watch out. (W1)", StringComparison.Ordinal)));
    }

    // ── cancel / Stop, delete / Remove ──

    [Fact]
    public async Task CliCancel_PostsCancelWithoutPrecheck()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var output = new FakeOutputHandler();
        var processor = new CancelTenantDeploymentProcessor_Orchestrator { DeploymentName = "tenant-test" };
        Init(processor, Pipeline(requests, _ => new HttpResponseMessage(HttpStatusCode.NoContent)), output);

        await processor.ProcessAsync();

        Assert.Equal((HttpMethod.Post, $"{Arm}/tenant-test/cancel{ApiVersion}"), (Assert.Single(requests).Method, requests[0].Uri));
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData("Succeeded", false)]
    [InlineData("Failed", false)]
    [InlineData("Running", true)]
    public async Task PsStop_CancelsOnlyRunningDeployment(string state, bool cancels)
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        UseClient(ClientType.PowerShell);
        var processor = new CancelTenantDeploymentProcessor_Orchestrator { DeploymentName = "tenant-test" };
        Init(processor, Pipeline(requests, r => r.Method == HttpMethod.Get ? Json(Deployment(state)) : new HttpResponseMessage(HttpStatusCode.NoContent)), new FakeOutputHandler());

        if (cancels)
        {
            await processor.ProcessAsync();
        }
        else
        {
            ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(processor.ProcessAsync);
            Assert.Equal("There is no running deployment called 'tenant-test' at the current tenant scope.", error.Message);
        }

        Assert.Equal(cancels ? [HttpMethod.Get, HttpMethod.Post] : [HttpMethod.Get], requests.Select(r => r.Method));
    }

    [Theory]
    [InlineData("Azure-AsyncOperation")]
    [InlineData("Operation-Location")]
    [InlineData("Location")]
    public async Task CliDelete_WaitsOnAsyncHeaderWithoutWritingOperationPayload(string headerName)
    {
        List<string> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add(request.RequestUri!.ToString());
            return requests.Count == 1
                ? WithHeaders(Json("""{ "status": "Accepted" }""", HttpStatusCode.Accepted), (headerName, "https://management.azure.com/providers/Microsoft.Resources/operations/delete-1"))
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var output = new FakeOutputHandler();
        var processor = new DeleteTenantDeploymentProcessor_Orchestrator { DeploymentName = "tenant-test" };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(headerName == "Location" ? 3 : 2, requests.Count);
        Assert.Equal("https://management.azure.com/providers/Microsoft.Resources/operations/delete-1", requests[1]);
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.NoContent, true)]
    public async Task PsRemove_ChecksExistenceBeforeDeleting(HttpStatusCode headStatus, bool deletes)
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        UseClient(ClientType.PowerShell);
        var processor = new DeleteTenantDeploymentProcessor_Orchestrator { DeploymentName = "tenant-test" };
        Init(processor, Pipeline(requests, r => new HttpResponseMessage(r.Method == HttpMethod.Head ? headStatus : HttpStatusCode.NoContent)), new FakeOutputHandler());

        if (deletes)
        {
            await processor.ProcessAsync();
        }
        else
        {
            ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(processor.ProcessAsync);
            Assert.Equal("Deployment 'tenant-test' doesn't exist in the current tenant.", error.Message);
        }

        Assert.Equal(deletes ? [HttpMethod.Head, HttpMethod.Delete] : [HttpMethod.Head], requests.Select(r => r.Method));
    }

    // ── list / show / export / wait ──

    [Fact]
    public async Task PsList_WritesEveryPageAsPSDeployment()
    {
        var output = new FakeOutputHandler();
        UseClient(ClientType.PowerShell);
        var processor = new ListTenantDeploymentProcessor_Orchestrator();
        Init(processor, Pipeline([], r => r.RequestUri!.Query.Contains("page=2")
            ? Json($$"""{ "value": [ {{Deployment("Failed").Replace("tenant-test", "two")}} ] }""")
            : Json($$"""{ "value": [ {{Deployment("Succeeded")}} ], "nextLink": "{{Arm}}{{ApiVersion}}&page=2" }""")), output);

        await processor.ProcessAsync();

        List<PSDeployment> deployments = Assert.IsType<List<PSDeployment>>(Assert.Single(output.WrittenValues));
        Assert.Equal(["tenant-test", "two"], deployments.Select(d => d.DeploymentName));
    }

    [Fact]
    public void PSDeployment_MapsLegacyShape()
    {
        DeploymentExtended deployment = JsonSerializer.Deserialize<DeploymentExtended>(Deployment("Succeeded"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        PSDeployment result = PSDeployment.From(deployment);

        Assert.Equal(("/providers/Microsoft.Resources/deployments/tenant-test", "tenant-test", "eastus", "corr-1"), (result.Id, result.DeploymentName, result.Location, result.CorrelationId));
        Assert.Equal(DeploymentMode.Incremental, result.Mode);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), result.Timestamp.ToUniversalTime());
        Assert.Equal("RequestContent", result.DeploymentDebugLogLevel);
        Assert.Equal(("String", "value"), (result.Parameters!["p"].Type, result.Parameters["p"].Value));
        Assert.Equal(3L, Convert.ToInt64(result.Outputs!["count"].Value));
        Assert.Equal(
            "\r\nName             Type                       Value     \r\n"
            + "===============  =========================  ==========\r\n"
            + "p                String                     \"value\"   \r\n",
            result.ParametersString!.ReplaceLineEndings("\r\n"));
    }

    [Theory]
    [InlineData("""{ "template": { "resources": [] } }""", "{\"resources\":[]}")]
    [InlineData("{}", "null")]
    public async Task CliExport_WritesOnlyTemplate(string body, string expected)
    {
        var output = new FakeOutputHandler();
        var processor = new ExportTenantDeploymentProcessor_Orchestrator { DeploymentName = "tenant-test" };
        Init(processor, Pipeline([], _ => Json(body)), output);

        await processor.ProcessAsync();

        object written = Assert.Single(output.WrittenValues);
        Assert.Equal(expected, written is SerializedJson json ? json.Value : JsonSerializer.Serialize(written));
    }

    [Fact]
    public async Task CliWait_RequiresCondition()
    {
        var processor = new WaitTenantDeploymentProcessor_Orchestrator { DeploymentName = "tenant-test" };
        Init(processor, Pipeline([]), new FakeOutputHandler());

        ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(processor.ProcessAsync);

        Assert.Equal("incorrect usage: --created | --updated | --deleted | --exists | --custom JMESPATH", error.Message);
    }

    [Theory]
    [InlineData("deleted", HttpStatusCode.NotFound, "Succeeded")]
    [InlineData("created", HttpStatusCode.OK, "Succeeded")]
    [InlineData("exists", HttpStatusCode.OK, "Running")]
    [InlineData("custom", HttpStatusCode.OK, "Running")]
    public async Task CliWait_ReturnsWhenConditionIsMet(string condition, HttpStatusCode status, string state)
    {
        var output = new FakeOutputHandler();
        var processor = new WaitTenantDeploymentProcessor_Orchestrator
        {
            DeploymentName = "tenant-test",
            Created = condition == "created",
            Deleted = condition == "deleted",
            Exists = condition == "exists",
            Custom = condition == "custom" ? "properties.provisioningState=='Running'" : null,
        };
        Init(processor, Pipeline([], _ => status == HttpStatusCode.OK ? Json(Deployment(state)) : Json("""{ "error": { "code": "DeploymentNotFound" } }""", status)), output);

        await processor.ProcessAsync();

        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task CliWait_FailedDeploymentRaisesAndTimeoutWritesEmptyObject()
    {
        var failed = new WaitTenantDeploymentProcessor_Orchestrator { DeploymentName = "tenant-test", Created = true };
        Init(failed, Pipeline([], _ => Json(Deployment("Failed"))), new FakeOutputHandler());
        var output = new FakeOutputHandler();
        var timeout = new WaitTenantDeploymentProcessor_Orchestrator { DeploymentName = "tenant-test", Created = true, Interval = 1, Timeout = 1 };
        Init(timeout, Pipeline([], _ => Json(Deployment("Running"))), output);

        Assert.Equal("The operation failed", (await Assert.ThrowsAsync<InvalidOperationException>(failed.ProcessAsync)).Message);
        await timeout.ProcessAsync();

        Assert.Equal("{}", Assert.IsType<SerializedJson>(Assert.Single(output.WrittenValues)).Value);
    }

    // ── Legacy error text ──

    [Fact]
    public async Task CliCreate_InvalidTemplateJsonRaisesLegacyParseError()
    {
        string path = Path.Combine(_dir, "broken.json");
        File.WriteAllText(path, """{ "resources": [ """);
        var processor = new CreateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = path };
        Init(processor, Pipeline([]), new FakeOutputHandler());

        ArgumentException error = await Assert.ThrowsAsync<ArgumentException>(processor.ProcessAsync);

        Assert.Equal($"Failed to parse '{path}', please check whether it is a valid JSON format", error.Message);
    }

    [Fact]
    public async Task CliCreate_AcceptsCommentsAndMultilineStringsInTemplate()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        string path = Path.Combine(_dir, "multiline.json");
        File.WriteAllText(path, "{\n  // comment\n  \"contentVersion\": \"1.0.0.0\",\n  \"resources\": [],\n  \"outputs\": { \"o\": { \"type\": \"string\", \"value\": \"line1\nline2\" } }\n}");
        var processor = new CreateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = path };
        Init(processor, Pipeline(requests), new FakeOutputHandler());

        await processor.ProcessAsync();

        JsonElement template = JsonDocument.Parse(requests[1].Body!).RootElement.GetProperty("properties").GetProperty("template");
        Assert.Equal("line1\nline2", template.GetProperty("outputs").GetProperty("o").GetProperty("value").GetString());
    }

    [Fact]
    public async Task PsNewWhatIf_ThrowsLegacyCloudErrorText()
    {
        var publisher = new FakeEventPublisher();
        var processor = new NewTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile(), WhatIf = true };
        Init(processor, Pipeline([], r => r.RequestUri!.AbsolutePath.EndsWith("/whatIf", StringComparison.Ordinal) ? Json(ValidationError, HttpStatusCode.BadRequest) : null), new FakeOutputHandler(), publisher);

        HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(processor.ProcessAsync);

        string nl = Environment.NewLine;
        Assert.Equal($"{nl}InvalidTemplate - The template is invalid.{nl}InvalidParameter - Bad parameter.", error.Message);
        Assert.Empty(FailureMessages(publisher));
    }

    [Fact]
    public async Task CliShow_RewritesHttpFailureToODataV4Text()
    {
        var publisher = new FakeEventPublisher();
        var processor = new GetTenantDeploymentProcessor_Orchestrator { DeploymentName = "missing" };
        Init(processor, Pipeline([], _ => Json(NotFound, HttpStatusCode.NotFound)), new FakeOutputHandler(), publisher);

        await Assert.ThrowsAsync<HttpRequestException>(processor.ProcessAsync);

        Assert.Equal(
            "(DeploymentNotFound) Deployment 'missing' could not be found.\nCode: DeploymentNotFound\nMessage: Deployment 'missing' could not be found.",
            Assert.Single(FailureMessages(publisher)));
    }

    [Fact]
    public async Task PsGet_ThrowsLegacyCloudExceptionTextOnce()
    {
        var publisher = new FakeEventPublisher();
        UseClient(ClientType.PowerShell);
        var processor = new GetTenantDeploymentProcessor_Orchestrator { DeploymentName = "missing" };
        Init(processor, Pipeline([], _ => WithHeaders(Json(NotFound, HttpStatusCode.NotFound), ("x-ms-request-id", "req-1"))), new FakeOutputHandler(), publisher);

        HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(processor.ProcessAsync);

        string nl = Environment.NewLine;
        Assert.Equal($"Deployment 'missing' could not be found.{nl}StatusCode: 404{nl}ReasonPhrase: Not Found{nl}OperationID : req-1", error.Message);
        Assert.Empty(FailureMessages(publisher));
    }

    // ── What-if formatter ──

    [Fact]
    public void WhatIfFormatter_CliMatchesLegacyText()
    {
        string text = TenantDeploymentWhatIfFormatter.Format(ToCliResult(WhatIfResult, null)!, cli: true, color: false);

        Assert.Equal(
            """
            Note: The result may contain false positive predictions (noise).
            You can help us improve the accuracy of the result by opening an issue here: https://aka.ms/WhatIfIssues
            NOTICE! - Want to get What-if without noise? Move to Deployment Stacks What-If https://aka.ms/stackswhatifGA

            Resource and property changes are indicated with these symbols:
              + Create
              = Nochange

            The deployment will update the following scope:

            Scope: /

              + Microsoft.Management/ManagementGroups/mg1 [2021-04-01]

                  apiVersion: "2021-04-01"
                  name:       "mg1"

              = Microsoft.Management/ManagementGroups/mg2

            Resource changes: 1 to create, 1 no change.
            """.ReplaceLineEndings("\n"),
            text);
    }

    [Fact]
    public void WhatIfFormatter_ResourceWithoutIdRendersInCliButThrowsInPowerShell()
    {
        JsonObject result = ToCliResult(
            """{ "status": "Succeeded", "properties": { "changes": [ { "changeType": "Create", "extension": { "name": "Kubernetes", "version": "1.0" }, "identifiers": { "name": "pod1" } } ] } }""",
            null)!;

        Assert.Contains("Kubernetes@1.0", TenantDeploymentWhatIfFormatter.Format(result, cli: true, color: false));
        Assert.Equal("input", Assert.Throws<ArgumentNullException>(() => TenantDeploymentWhatIfFormatter.Format(result, cli: false, color: false)).ParamName);
    }

    // ── Helpers ──

    private const string ValidationError = """
        {
          "error": {
            "code": "InvalidTemplate",
            "message": "The template is invalid.",
            "details": [ { "code": "InvalidParameter", "message": "Bad parameter." } ]
          }
        }
        """;

    private const string NotFound = """{ "error": { "code": "DeploymentNotFound", "message": "Deployment 'missing' could not be found." } }""";

    private const string WhatIfResult = """
        {
          "status": "Succeeded",
          "properties": {
            "changes": [
              {
                "resourceId": "/providers/Microsoft.Management/managementGroups/mg1",
                "changeType": "Create",
                "after": { "apiVersion": "2021-04-01", "name": "mg1" }
              },
              { "resourceId": "/providers/Microsoft.Management/managementGroups/mg2", "changeType": "NoChange" }
            ]
          }
        }
        """;

    private static string Deployment(string state) => $$"""
        {
          "id": "/providers/Microsoft.Resources/deployments/tenant-test",
          "name": "tenant-test",
          "location": "eastus",
          "properties": {
            "provisioningState": "{{state}}",
            "mode": "Incremental",
            "correlationId": "corr-1",
            "timestamp": "2026-01-02T03:04:05Z",
            "debugSetting": { "detailLevel": "RequestContent" },
            "parameters": { "p": { "type": "String", "value": "value" } },
            "outputs": { "count": { "type": "Int", "value": 3 } },
            "unmodeled": "extra",
            "error": {
              "code": "DeploymentFailed",
              "message": "At least one resource deployment operation failed.",
              "details": [ { "code": "Conflict", "message": "The resource is busy." } ]
            }
          }
        }
        """;

    private string TemplateFile(string name = "template.json", string parameters = "{}")
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, $$"""
            {
              "$schema": "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
              "contentVersion": "1.0.0.0",
              "parameters": {{parameters}},
              "resources": []
            }
            """);
        return path;
    }

    private static Dictionary<string, object> Template() => new()
    {
        ["$schema"] = "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
        ["contentVersion"] = "1.0.0.0",
        ["resources"] = Array.Empty<object>(),
    };

    // Routes by request: validate → success, operations → empty page, anything else → the deployment.
    // `route` overrides a request by returning a response (null falls back to the default).
    private static FakeHttpPipeline Pipeline(
        List<(HttpMethod Method, string Uri, string? Body)> requests,
        Func<HttpRequestMessage, HttpResponseMessage?>? route = null) =>
        new((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!.ToString(), request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()));
            string path = request.RequestUri!.AbsolutePath;
            return route?.Invoke(request)
                ?? (path.EndsWith("/validate", StringComparison.Ordinal) ? Json("""{ "properties": { "provisioningState": "Succeeded" } }""")
                : path.EndsWith("/operations", StringComparison.Ordinal) ? Json("""{ "value": [] }""")
                : Json(Deployment("Succeeded")));
        });

    private static void Init(IProcessor processor, FakeHttpPipeline pipeline, FakeOutputHandler output, IEventPublisher? publisher = null)
    {
        pipeline.PollingInterval = 0;
        Profile profile = new() { TenantId = "test-tenant", SubscriptionId = "test-subscription" };
        processor.Init(new ProcessorContext(pipeline, publisher ?? new FakeEventPublisher(), new FakeAuthHandler(profile), output, Profile: profile));
    }

    private static List<string> FailureMessages(FakeEventPublisher publisher) =>
        publisher.Messages.Where(m => m.Id == Events.ResourceOperationFailed).Select(m => m.Text).ToList();

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage WithHeaders(HttpResponseMessage response, params (string Name, string Value)[] headers)
    {
        foreach ((string name, string value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }
        return response;
    }

    private static JsonObject? ToCliResult(string rawBody, string[]? excludeChangeTypes) =>
        (JsonObject?)typeof(WhatIfTenantDeploymentProcessor_Orchestrator)
            .GetMethod("ToCliResult", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [rawBody, excludeChangeTypes]);

    private static string RequestUri(IProcessor processor) =>
        (string)processor.GetType()
            .GetMethod("CreateRequestUri", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(processor, null)!;
}
