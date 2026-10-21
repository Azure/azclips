// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
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
        Assert.Equal(Arm + ApiVersion, RequestUri(new ListTenantDeploymentProcessor_Orchestrator {}));
        Assert.Equal(
            Arm + ApiVersion + "&$filter=provisioningState%20eq%20%27Succeeded%27%20%26%20name%20eq%20%27a%2Fb%27",
            RequestUri(new ListTenantDeploymentProcessor_Orchestrator { _filter = "provisioningState eq 'Succeeded' & name eq 'a/b'" }));
    }

    [Fact]
    public void ExtensionIndex_ExposesTenantCommands()
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
                "deployment tenant what-if",
            ],
            index.GetCommandKeys(ClientType.CLI).Where(key => key.StartsWith("deployment tenant ", StringComparison.Ordinal)).Order().ToArray());
        Assert.Equal(
            [
                "Get-AzTenantDeployment_Get",
                "Get-AzTenantDeployment_GetById",
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
            CommandMetadata metadata = index.GetCommandMetadata(command, ClientType.CLI);
            Assert.DoesNotContain("no-wait", metadata.Parameters.Keys);
        }

        // create/validate/what-if and New/Test use the resource group deployment input names plus location, without mode (always Incremental, like legacy).
        string[] cliInputs = ["location", "name", "parameter", "parameters-file", "parameters-uri", "profile", "query-string", "template-file", "template-spec-id", "template-uri", "validation-level"];
        CommandMetadata create = index.GetCommandMetadata("deployment tenant create", ClientType.CLI);
        CommandMetadata validate = index.GetCommandMetadata("deployment tenant validate", ClientType.CLI);
        CommandMetadata whatIf = index.GetCommandMetadata("deployment tenant what-if", ClientType.CLI);
        Assert.Equal(Sorted(cliInputs), Sorted(create.Parameters.Keys));
        Assert.Equal(Sorted(cliInputs), Sorted(validate.Parameters.Keys));
        Assert.Equal(Sorted([.. cliInputs, "result-format"]), Sorted(whatIf.Parameters.Keys));
        Assert.Empty(create.ParameterAliases);
        Assert.True(create.Parameters["name"].IsRequired);
        Assert.False(validate.Parameters["name"].IsRequired);

        string[] psInputs = ["Location", "Name", "Parameter", "ParametersFile", "ParametersUri", "Profile", "QueryString", "TemplateFile", "TemplateSpecId", "TemplateUri", "ValidationLevel"];
        CommandMetadata newPs = index.GetCommandMetadata("New-AzTenantDeployment", ClientType.PowerShell);
        CommandMetadata testPs = index.GetCommandMetadata("Test-AzTenantDeployment", ClientType.PowerShell);
        Assert.Equal(typeof(CreateTenantDeploymentProcessor_Orchestrator), newPs.ProcessorType);
        Assert.Equal(Sorted(psInputs), Sorted(newPs.Parameters.Keys));
        Assert.Equal(Sorted([.. psInputs, "TemplateObject"]), Sorted(testPs.Parameters.Keys));
        Assert.Equal(["TemplateParameterFile"], testPs.Parameters["ParametersFile"].Aliases);

        // Get by id, Stop and Remove also take -Id.
        string[] byIdCommands = ["Get-AzTenantDeployment_GetById", "Stop-AzTenantDeployment", "Remove-AzTenantDeployment"];
        foreach (string command in index.GetCommandKeys(ClientType.PowerShell).Where(key => key.Contains("TenantDeployment", StringComparison.Ordinal)))
        {
            Assert.Equal(byIdCommands.Contains(command), index.GetCommandMetadata(command, ClientType.PowerShell).Parameters.ContainsKey("Id"));
        }
    }

    // ── create / New, validate / Test, what-if ──

    [Fact]
    public async Task Create_ValidatesThenPutsDeploymentAndWritesResult()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var output = new FakeOutputHandler();
        var processor = new CreateTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateFile = TemplateFile(),
            ValidationLevel = "Template",
        };
        Init(processor, Pipeline(requests), output);

        await processor.ProcessAsync();

        Assert.Equal((HttpMethod.Post, $"{Arm}/tenant-test/validate{ApiVersion}"), (requests[0].Method, requests[0].Uri));
        Assert.Equal((HttpMethod.Put, $"{Arm}/tenant-test{ApiVersion}"), (requests[1].Method, requests[1].Uri));
        Assert.Equal(requests[0].Body, requests[1].Body);
        JsonElement body = JsonDocument.Parse(requests[1].Body!).RootElement;
        Assert.Equal("eastus", body.GetProperty("location").GetString());
        JsonElement properties = body.GetProperty("properties");
        Assert.Equal(("Incremental", "Template"), (properties.GetProperty("mode").GetString(), properties.GetProperty("validationLevel").GetString()));
        Assert.Equal("1.0.0.0", properties.GetProperty("template").GetProperty("contentVersion").GetString());
        Assert.Equal("tenant-test", Assert.IsType<DeploymentExtended>(Assert.Single(output.WrittenValues)).Name);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.OK)]
    public async Task Create_ValidationErrorThrowsWithoutPut(HttpStatusCode status)
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var processor = new CreateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline(requests, r => r.RequestUri!.AbsolutePath.EndsWith("/validate") ? Json(ValidationError, status) : null), new FakeOutputHandler());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);

        Assert.Equal(ValidationFailedMessage, error.Message.ReplaceLineEndings("\n"));
        Assert.Single(requests);
    }

    [Fact]
    public async Task Create_RequiresLocation()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var processor = new CreateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", TemplateFile = TemplateFile() };
        Init(processor, Pipeline(requests), new FakeOutputHandler());

        Exception error = await Assert.ThrowsAnyAsync<Exception>(processor.ProcessAsync);

        Assert.Contains("Location must be provided.", error.Message);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task Validate_DefaultsNameToGuidAndWritesResult()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var output = new FakeOutputHandler();
        var processor = new ValidateTenantDeploymentProcessor_Orchestrator { Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline(requests), output);

        await processor.ProcessAsync();

        Uri uri = new(Assert.Single(requests).Uri);
        Assert.Equal("validate", uri.Segments[^1]);
        Assert.True(Guid.TryParse(uri.Segments[^2].TrimEnd('/'), out _));
        Assert.Equal("Succeeded", Assert.IsType<DeploymentValidateResult>(Assert.Single(output.WrittenValues)).Properties?.ProvisioningState);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.OK)]
    public async Task Validate_ErrorThrows(HttpStatusCode status)
    {
        var output = new FakeOutputHandler();
        var processor = new ValidateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline([], _ => Json(ValidationError, status)), output);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);

        Assert.Equal(ValidationFailedMessage, error.Message.ReplaceLineEndings("\n"));
        Assert.Empty(output.WrittenValues);
    }

    // The scope-neutral builder owns these inputs; each one must still reach the request body.
    [Theory]
    [InlineData("local")]
    [InlineData("template-uri")]
    [InlineData("template-spec")]
    public async Task Validate_SendsSharedTemplateAndParameterInputs(string source)
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var processor = new ValidateTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", ValidationLevel = " Provider " };
        const string TemplateSpecId = "/subscriptions/s/resourceGroups/rg/providers/Microsoft.Resources/templateSpecs/ts/versions/1";
        switch (source)
        {
            case "local":
                string parametersFile = Path.Combine(_dir, "parameters.json");
                File.WriteAllText(parametersFile, """{ "parameters": { "count": { "value": 1 }, "label": { "value": "a" } } }""");
                processor.TemplateObject = new Hashtable { ["contentVersion"] = "1.0.0.0", ["resources"] = Array.Empty<object>() };
                processor.ParametersFile = parametersFile;
                processor.Parameter = ["COUNT=3"];
                break;
            case "template-uri":
                processor.TemplateUri = "https://example.com/template.json";
                processor.QueryString = "?sv=1";
                processor.ParametersUri = "https://example.com/parameters.json";
                break;
            default:
                processor.TemplateSpecId = TemplateSpecId;
                break;
        }
        Init(processor, Pipeline(requests), new FakeOutputHandler());

        await processor.ProcessAsync();

        JsonElement properties = JsonDocument.Parse(Assert.Single(requests).Body!).RootElement.GetProperty("properties");
        Assert.Equal("Provider", properties.GetProperty("validationLevel").GetString());
        switch (source)
        {
            case "local":
                Assert.Equal("1.0.0.0", properties.GetProperty("template").GetProperty("contentVersion").GetString());
                JsonElement parameters = properties.GetProperty("parameters");
                Assert.Equal(3, parameters.GetProperty("count").GetProperty("value").GetInt32());
                Assert.Equal("a", parameters.GetProperty("label").GetProperty("value").GetString());
                break;
            case "template-uri":
                JsonElement templateLink = properties.GetProperty("templateLink");
                Assert.Equal(("https://example.com/template.json", "sv=1"), (templateLink.GetProperty("uri").GetString(), templateLink.GetProperty("queryString").GetString()));
                Assert.Equal("https://example.com/parameters.json", properties.GetProperty("parametersLink").GetProperty("uri").GetString());
                break;
            default:
                Assert.Equal(TemplateSpecId, properties.GetProperty("templateLink").GetProperty("id").GetString());
                break;
        }
    }

    [Fact]
    public async Task WhatIf_SendsIncrementalModeAndResultFormatAndWritesResult()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var output = new FakeOutputHandler();
        var processor = new WhatIfTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateFile = TemplateFile(),
            ResultFormat = "resourceidonly",
        };
        Init(processor, Pipeline(requests, _ => Json(WhatIfResult)), output);

        await processor.ProcessAsync();

        Assert.Equal((HttpMethod.Post, $"{Arm}/tenant-test/whatIf{ApiVersion}"), (Assert.Single(requests).Method, requests[0].Uri));
        JsonElement body = JsonDocument.Parse(requests[0].Body!).RootElement;
        Assert.Equal("eastus", body.GetProperty("location").GetString());
        Assert.Equal("Incremental", body.GetProperty("properties").GetProperty("mode").GetString());
        Assert.Equal("ResourceIdOnly", body.GetProperty("properties").GetProperty("whatIfSettings").GetProperty("resultFormat").GetString());
        Assert.Equal(2, Assert.IsType<WhatIfOperationResult>(Assert.Single(output.WrittenValues)).Properties?.Changes?.Count);
    }

    [Fact]
    public async Task WhatIf_PolledFailedStatusThrows()
    {
        var output = new FakeOutputHandler();
        var processor = new WhatIfTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile() };
        Init(processor, Pipeline([], r => r.Method == HttpMethod.Post
            ? WithHeaders(Json("", HttpStatusCode.Accepted), ("Location", "https://management.azure.com/providers/Microsoft.Resources/operations/whatif-location"))
            : Json("""{ "status": "Failed", "error": { "code": "InvalidTemplate", "message": "bad" } }""")), output);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(processor.ProcessAsync);

        Assert.Equal("What-if operation failed.\n  InvalidTemplate: bad", error.Message.ReplaceLineEndings("\n"));
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task WhatIf_RejectsUnknownResultFormat()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var processor = new WhatIfTenantDeploymentProcessor_Orchestrator { Name = "tenant-test", Location = "eastus", TemplateFile = TemplateFile(), ResultFormat = "Bogus" };
        Init(processor, Pipeline(requests), new FakeOutputHandler());

        Exception error = await Assert.ThrowsAnyAsync<Exception>(processor.ProcessAsync);

        Assert.Contains("ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'.", error.Message);
        Assert.Empty(requests);
    }
    // ── cancel / Stop, delete / Remove ──

    [Fact]
    public async Task Cancel_PostsCancelWithoutPrecheck()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var output = new FakeOutputHandler();
        var processor = new CancelAtTenantScopeDeploymentsProcessor { DeploymentName = "tenant-test" };
        Init(processor, Pipeline(requests, _ => new HttpResponseMessage(HttpStatusCode.NoContent)), output);

        await processor.ProcessAsync();

        Assert.Equal((HttpMethod.Post, $"{Arm}/tenant-test/cancel{ApiVersion}"), (Assert.Single(requests).Method, requests[0].Uri));
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public async Task Delete_MissingDeploymentSucceedsWithoutPrecheckOrOutput(ClientType clientType)
    {
        UseClient(clientType);
        List<(HttpMethod Method, string Uri)> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!.ToString()));
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var output = new FakeOutputHandler();
        var processor = new DeleteTenantDeploymentProcessor_Orchestrator { DeploymentName = "does-not-exist" };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal((HttpMethod.Delete, $"{Arm}/does-not-exist{ApiVersion}"), Assert.Single(requests));
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData("Azure-AsyncOperation")]
    [InlineData("Operation-Location")]
    [InlineData("Location")]
    public async Task Delete_WaitsOnAsyncHeaderWithoutWritingOperationPayload(string headerName)
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
        var processor = new DeleteAtTenantScopeDeploymentsProcessor { DeploymentName = "tenant-test" };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(headerName == "Location" ? 3 : 2, requests.Count);
        Assert.Equal("https://management.azure.com/providers/Microsoft.Resources/operations/delete-1", requests[1]);
        Assert.Empty(output.WrittenValues);
    }

    // ── list / show / export ──

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

    [Theory]
    [InlineData(typeof(GetTenantDeploymentProcessor_Orchestrator), "GET")]
    [InlineData(typeof(CancelTenantDeploymentProcessor_Orchestrator), "POST")]
    [InlineData(typeof(DeleteTenantDeploymentProcessor_Orchestrator), "DELETE")]
    public async Task ById_SendsRequestForParsedName(Type type, string method)
    {
        var (pipeline, output, sent) = DeploymentProcessorTestKit.Fake(_ => DeploymentProcessorTestKit.Status(HttpStatusCode.NoContent));
        var processor = (IProcessor)Activator.CreateInstance(type)!;
        DeploymentProcessorTestKit.Configure(processor, pipeline, output, p =>
            type.GetProperty("Id")!.SetValue(p, "/providers/Microsoft.Resources/deployments/dep/"));

        await processor.ProcessAsync();

        Assert.Equal(method, sent[0].Method.Method);
        Assert.StartsWith($"{Arm}/dep", sent[0].Uri);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("not-a-resource-id")]
    [InlineData("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Resources/deployments/dep")]
    [InlineData("/providers/Microsoft.Management/managementGroups/test-mg/providers/Microsoft.Resources/deployments/dep")]
    [InlineData("/providers/Microsoft.Compute/virtualMachines/vm1")]
    public async Task ById_MalformedId_ThrowsWithoutRequest(string id)
    {
        var (pipeline, output, sent) = DeploymentProcessorTestKit.Fake(_ => DeploymentProcessorTestKit.Status(HttpStatusCode.NoContent));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DeploymentProcessorTestKit.Configure(new DeleteTenantDeploymentProcessor_Orchestrator(), pipeline, output, p => p.Id = id).ProcessAsync());
        Assert.Empty(sent);
    }

    private const string ValidationFailedMessage = "Deployment validation failed.\n  InvalidTemplate: The template is invalid.\n    InvalidParameter: Bad parameter.";

    private static string[] Sorted(IEnumerable<string> keys) => keys.Order(StringComparer.Ordinal).ToArray();
    private static string RequestUri(IProcessor processor) =>
        (string)processor.GetType()
            .GetMethod("CreateRequestUri", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(processor, null)!;
}
