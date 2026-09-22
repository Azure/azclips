// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Customization;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cli.Tests;

public class ManagementGroupDeploymentTests
{
    [Fact]
    public void HttpRuntime_PreservesSevenArgumentSendAsyncLroOverload()
    {
        int[] parameterCounts = typeof(HttpRuntime)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(HttpRuntime.SendAsyncLRO))
            .Select(method => method.GetParameters().Length)
            .Order()
            .ToArray();

        Assert.Equal([7, 8], parameterCounts);
    }

    [Fact]
    public void Create_PowerShellMetadataMapsModeToInput()
    {
        CommandMetadata metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata("New-AzManagementGroupDeployment", ClientType.PowerShell);

        Assert.Equal("ModeInput", metadata.Parameters["Mode"].Name);
        Assert.Equal(["Incremental", "Complete"], metadata.Parameters["Mode"].AllowedValues);
    }

    [Theory]
    [InlineData("deployment mg create")]
    [InlineData("deployment mg validate")]
    public void CliMetadataMapsAdvertisedShortAliases(string command)
    {
        CommandMetadata metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata(command, ClientType.CLI);

        Assert.Equal("management-group-id", metadata.ParameterAliases["m"]);
        Assert.Equal("name", metadata.ParameterAliases["n"]);
        Assert.Equal("location", metadata.ParameterAliases["l"]);
        Assert.Equal("template-file", metadata.ParameterAliases["f"]);
        Assert.Equal("template-uri", metadata.ParameterAliases["u"]);
        Assert.Equal("template-spec", metadata.ParameterAliases["s"]);
        Assert.Equal("parameters", metadata.ParameterAliases["p"]);
    }

    [Fact]
    public async Task Create_SerializesNestedPowerShellHashtableTemplate()
    {
        List<string> requestBodies = [];
        FakeHttpPipeline pipeline = new((request, _) =>
        {
            requestBodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            return Json("""{"properties":{"provisioningState":"Succeeded"}}""");
        });
        NewManagementGroupDeploymentProcessor_Orchestrator processor = CreateProcessor(
            new NewManagementGroupDeploymentProcessor_Orchestrator { NoWait = true },
            pipeline,
            new FakeOutputHandler());
        processor.TemplateObject = new Hashtable
        {
            ["contentVersion"] = "1.0.0.0",
            ["variables"] = new Hashtable
            {
                ["settings"] = new Hashtable { ["enabled"] = true },
            },
            ["resources"] = Array.Empty<object>(),
        };

        await processor.ProcessAsync();

        Dictionary<string, object?> template =
            Assert.IsType<Dictionary<string, object?>>(processor.Template);
        Dictionary<string, object?> variables =
            Assert.IsType<Dictionary<string, object?>>(template["variables"]);
        Assert.IsType<Dictionary<string, object?>>(variables["settings"]);

        Assert.Equal(2, requestBodies.Count);
        foreach (string requestBody in requestBodies)
        {
            using JsonDocument document = JsonDocument.Parse(requestBody);
            Assert.True(document.RootElement.GetProperty("properties")
                .GetProperty("template")
                .GetProperty("variables")
                .GetProperty("settings")
                .GetProperty("enabled")
                .GetBoolean());
        }
    }

    [Theory]
    [InlineData("""{"parametersJson":"{\"parameters\":{\"name\":{\"value\":\"test\"}}}"}""")]
    [InlineData("""{"parameters":{"name":{"value":"test"}}}""")]
    public void BicepParametersOutputSupportsEnvelopeAndArmDocument(string output)
    {
        Type preparation = typeof(NewManagementGroupDeploymentProcessor_Orchestrator).Assembly.GetType(
            "Microsoft.Azure.Azclips.Extensions.Resources.Processors.ManagementGroupDeploymentPreparation")!;
        MethodInfo extract = preparation.GetMethod(
            "ExtractBicepParametersJson",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        string json = (string)extract.Invoke(null, [output])!;

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("test", document.RootElement.GetProperty("parameters")
            .GetProperty("name")
            .GetProperty("value")
            .GetString());
    }

    [Fact]
    public void Get_CliMetadataMapsShortAliases()
    {
        Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex extension = new();
        CommandMetadata show = extension.GetCommandMetadata("deployment mg show", ClientType.CLI);
        CommandMetadata list = extension.GetCommandMetadata("deployment mg list", ClientType.CLI);

        Assert.Equal("management-group-id", show.ParameterAliases["m"]);
        Assert.Equal("name", show.ParameterAliases["n"]);
        Assert.Equal("management-group-id", list.ParameterAliases["m"]);
    }

    [Fact]
    public void WhatIf_MetadataExposesMode()
    {
        CommandMetadata metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata("deployment mg what-if", ClientType.CLI);

        Assert.Equal("ModeInput", metadata.Parameters["mode"].Name);
        Assert.Equal(["Incremental", "Complete"], metadata.Parameters["mode"].AllowedValues);
    }

    [Fact]
    public async Task Create_ValidatesBeforeSendingPut()
    {
        List<(HttpMethod Method, string Path)> requests = [];
        FakeHttpPipeline pipeline = new((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            return Json(request.Method == HttpMethod.Post
                ? """{"properties":{"provisioningState":"Succeeded"}}"""
                : """{"properties":{"provisioningState":"Succeeded"}}""");
        });
        FakeOutputHandler output = new();
        NewManagementGroupDeploymentProcessor_Orchestrator processor = CreateProcessor(
            new NewManagementGroupDeploymentProcessor_Orchestrator { NoWait = true },
            pipeline,
            output);

        await processor.ProcessAsync();

        Assert.Collection(
            requests,
            request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.EndsWith("/validate", request.Path);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                Assert.EndsWith("/deployments/test-deployment", request.Path);
            });
        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task Create_DoesNotSendPutWhenValidationReturnsEmbeddedError()
    {
        FakeHttpPipeline pipeline = new((_, _) =>
            Json("""{"error":{"code":"InvalidTemplate","message":"Template is invalid."}}"""));
        NewManagementGroupDeploymentProcessor_Orchestrator processor = CreateProcessor(
            new NewManagementGroupDeploymentProcessor_Orchestrator { NoWait = true },
            pipeline,
            new FakeOutputHandler());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());

        Assert.Equal(1, pipeline.SendCount);
        Assert.Contains("InvalidTemplate: Template is invalid.", exception.Message);
    }

    [Fact]
    public async Task Create_RecognizesParameterUriWithQueryString()
    {
        const string parametersUri = "https://example.test/parameters.json?sv=1&sig=abc=def";
        List<string> requestBodies = [];
        FakeHttpPipeline pipeline = new((request, _) =>
        {
            requestBodies.Add(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            return Json("""{"properties":{"provisioningState":"Succeeded"}}""");
        });
        NewManagementGroupDeploymentProcessor_Orchestrator processor = CreateProcessor(
            new NewManagementGroupDeploymentProcessor_Orchestrator
            {
                NoWait = true,
                Parameter = [parametersUri],
            },
            pipeline,
            new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(2, requestBodies.Count);
        foreach (string requestBody in requestBodies)
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(requestBody);
            Assert.Equal(parametersUri, document.RootElement.GetProperty("properties")
                .GetProperty("parametersLink").GetProperty("uri").GetString());
        }
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    public async Task Create_RejectsNumericMode(string mode)
    {
        FakeHttpPipeline pipeline = new((_, _) =>
            Json("""{"properties":{"provisioningState":"Succeeded"}}"""));
        NewManagementGroupDeploymentProcessor_Orchestrator processor = CreateProcessor(
            new NewManagementGroupDeploymentProcessor_Orchestrator
            {
                ModeInput = mode,
            },
            pipeline,
            new FakeOutputHandler());

        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());

        Assert.Contains("Incremental or Complete", exception.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task Create_RejectsWhitespaceOnlyInlineParameterName()
    {
        FakeHttpPipeline pipeline = new((_, _) =>
            Json("""{"properties":{"provisioningState":"Succeeded"}}"""));
        NewManagementGroupDeploymentProcessor_Orchestrator processor = CreateProcessor(
            new NewManagementGroupDeploymentProcessor_Orchestrator
            {
                Parameter = [" =value"],
            },
            pipeline,
            new FakeOutputHandler());

        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync());

        Assert.Contains("key is empty", exception.Message);
        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task Validate_SendsOnlyValidationRequest()
    {
        List<(HttpMethod Method, string Path)> requests = [];
        FakeHttpPipeline pipeline = new((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            return Json("""{"properties":{"provisioningState":"Succeeded"}}""");
        });
        FakeOutputHandler output = new();
        TestManagementGroupDeploymentProcessor_Orchestrator processor = CreateProcessor(
            new TestManagementGroupDeploymentProcessor_Orchestrator(),
            pipeline,
            output);

        await processor.ProcessAsync();

        (HttpMethod method, string path) = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.EndsWith("/validate", path);
        Assert.Single(output.WrittenValues);
    }

    [Fact]
    public async Task GetAndList_UseManagementGroupScopeAndListUnwrapsPages()
    {
        string? getUri = null;
        FakeOutputHandler getOutput = new();
        GetAtManagementGroupScopeDeploymentsProcessor get =
            CreateProcessor(new GetAtManagementGroupScopeDeploymentsProcessor(), new FakeHttpPipeline((request, _) =>
            {
                getUri = request.RequestUri!.AbsoluteUri;
                return Json("""{"name":"test-deployment","properties":{"provisioningState":"Succeeded"}}""");
            }), getOutput);
        get.ManagementGroupId = "test-management-group";
        get.Name = "test-deployment";

        await get.ProcessAsync();

        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Management/managementGroups/test-management-group/providers/Microsoft.Resources/deployments/test-deployment?api-version=2025-04-01",
            getUri);
        Assert.Equal("test-deployment", Assert.IsType<DeploymentExtended>(Assert.Single(getOutput.WrittenValues)).Name);

        List<string> listUris = [];
        FakeOutputHandler listOutput = new();
        ListAtManagementGroupScopeDeploymentsProcessor list =
            CreateProcessor(new ListAtManagementGroupScopeDeploymentsProcessor(), new FakeHttpPipeline((request, _) =>
            {
                listUris.Add(request.RequestUri!.AbsoluteUri);
                return listUris.Count == 1
                    ? Json("""{"value":[{"name":"one"}],"nextLink":"https://management.azure.com/next-page"}""")
                    : Json("""{"value":[{"name":"two"}]}""");
            }), listOutput);
        list.ManagementGroupId = "test-management-group";

        await list.ProcessAsync();

        Assert.Equal(2, listUris.Count);
        Assert.Equal("https://management.azure.com/next-page", listUris[1]);
        List<DeploymentExtended> deployments =
            Assert.IsAssignableFrom<IEnumerable<DeploymentExtended>>(Assert.Single(listOutput.WrittenValues)).ToList();
        Assert.Equal(["one", "two"], deployments.Select(deployment => deployment.Name));
    }

    [Fact]
    public async Task List_IncludesEncodedFilterAndTopQueryParameters()
    {
        string? requestUri = null;
        ListAtManagementGroupScopeDeploymentsProcessor list =
            CreateProcessor(new ListAtManagementGroupScopeDeploymentsProcessor(), new FakeHttpPipeline((request, _) =>
            {
                requestUri = request.RequestUri!.AbsoluteUri;
                return Json("""{"value":[]}""");
            }), new FakeOutputHandler());
        list.ManagementGroupId = "test-management-group";
        list.Filter = "properties/provisioningState eq 'Succeeded'";
        list.Top = 5;

        await list.ProcessAsync();

        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Management/managementGroups/test-management-group/providers/Microsoft.Resources/deployments?api-version=2025-04-01&$filter=properties%2FprovisioningState%20eq%20%27Succeeded%27&$top=5",
            requestUri);
    }

    [Fact]
    public async Task Cancel_SendsExpectedRequestAndProducesNoOutput()
    {
        HttpMethod? sentMethod = null;
        string? sentUri = null;
        bool hadContent = true;
        FakeHttpPipeline pipeline = new((request, _) =>
        {
            sentMethod = request.Method;
            sentUri = request.RequestUri!.AbsoluteUri;
            hadContent = request.Content is not null;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        FakeOutputHandler output = new();
        CancelAtManagementGroupScopeDeploymentsProcessor processor =
            CreateProcessor(new CancelAtManagementGroupScopeDeploymentsProcessor(), pipeline, output);
        processor.ManagementGroupId = "test-management-group";
        processor.Name = "deployment_(blue)";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.Equal(HttpMethod.Post, sentMethod);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Management/managementGroups/test-management-group/providers/Microsoft.Resources/deployments/deployment_%28blue%29/cancel?api-version=2025-04-01",
            sentUri);
        Assert.False(hadContent);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public void Cancel_CliMetadataMapsAdvertisedShortAliases()
    {
        CommandMetadata metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata("deployment mg cancel", ClientType.CLI);

        Assert.Equal("management-group-id", metadata.ParameterAliases["m"]);
        Assert.Equal("name", metadata.ParameterAliases["n"]);
    }

    [Fact]
    public async Task Cancel_ArmFailureIsSurfacedWithoutOutput()
    {
        FakeHttpPipeline pipeline = new((_, _) =>
            Json(
                """{"error":{"code":"DeploymentNotActive","message":"The deployment is not active."}}""",
                HttpStatusCode.Conflict));
        FakeOutputHandler output = new();
        CancelAtManagementGroupScopeDeploymentsProcessor processor =
            CreateProcessor(new CancelAtManagementGroupScopeDeploymentsProcessor(), pipeline, output);
        processor.ManagementGroupId = "test-management-group";
        processor.Name = "test-deployment";

        HttpRequestException exception =
            await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Contains("DeploymentNotActive", exception.Message);
        Assert.Equal(1, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Delete_NoWaitSendsExpectedRequestWithoutPollingOrOutput()
    {
        HttpMethod? sentMethod = null;
        string? sentUri = null;
        bool hadContent = true;
        FakeHttpPipeline pipeline = new((request, _) =>
        {
            sentMethod = request.Method;
            sentUri = request.RequestUri!.AbsoluteUri;
            hadContent = request.Content is not null;
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });
        FakeOutputHandler output = new();
        RemoveManagementGroupDeploymentProcessor_Orchestrator processor =
            CreateProcessor(new RemoveManagementGroupDeploymentProcessor_Orchestrator { NoWait = true }, pipeline, output);
        processor.ManagementGroupId = "test-management-group";
        processor.Name = "deployment_(blue)";

        await processor.ProcessAsync();

        Assert.Equal(1, pipeline.SendCount);
        Assert.Equal(HttpMethod.Delete, sentMethod);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Management/managementGroups/test-management-group/providers/Microsoft.Resources/deployments/deployment_%28blue%29?api-version=2025-04-01",
            sentUri);
        Assert.False(hadContent);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public void Delete_CliMetadataMapsAdvertisedShortAliases()
    {
        CommandMetadata metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata("deployment mg delete", ClientType.CLI);

        Assert.Equal("management-group-id", metadata.ParameterAliases["m"]);
        Assert.Equal("name", metadata.ParameterAliases["n"]);
    }

    [Fact]
    public async Task Delete_WaitsOnLocationAndSuppressesOperationBody()
    {
        List<(HttpMethod Method, string Uri)> requests = [];
        FakeHttpPipeline pipeline = new((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsoluteUri));
            if (requests.Count == 1)
            {
                HttpResponseMessage accepted = new(HttpStatusCode.Accepted);
                accepted.Headers.Location = new Uri(
                    "https://management.azure.com/operations/delete-1?api-version=2025-04-01");
                return accepted;
            }

            return Json("""{"status":"Succeeded"}""");
        });
        FakeOutputHandler output = new();
        RemoveManagementGroupDeploymentProcessor_Orchestrator processor =
            CreateProcessor(new RemoveManagementGroupDeploymentProcessor_Orchestrator(), pipeline, output);
        processor.ManagementGroupId = "test-management-group";
        processor.Name = "test-deployment";

        await processor.ProcessAsync();

        Assert.Collection(
            requests,
            request =>
            {
                Assert.Equal(HttpMethod.Delete, request.Method);
                Assert.Contains("/deployments/test-deployment?api-version=2025-04-01", request.Uri);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal(
                    "https://management.azure.com/operations/delete-1?api-version=2025-04-01",
                    request.Uri);
            });
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Delete_PollingFailureIsSurfacedWithoutOutput()
    {
        int sendCount = 0;
        FakeHttpPipeline pipeline = new((_, _) =>
        {
            if (sendCount++ == 0)
            {
                HttpResponseMessage accepted = new(HttpStatusCode.Accepted);
                accepted.Headers.Location = new Uri("https://management.azure.com/operations/delete-1");
                return accepted;
            }

            return Json(
                """{"error":{"code":"DeleteFailed","message":"The deployment could not be deleted."}}""",
                HttpStatusCode.InternalServerError);
        });
        FakeOutputHandler output = new();
        RemoveManagementGroupDeploymentProcessor_Orchestrator processor =
            CreateProcessor(new RemoveManagementGroupDeploymentProcessor_Orchestrator(), pipeline, output);
        processor.ManagementGroupId = "test-management-group";
        processor.Name = "test-deployment";

        HttpRequestException exception =
            await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Contains("DeleteFailed", exception.Message);
        Assert.Equal(2, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Export_SendsExpectedRequestAndWritesTemplateOnly()
    {
        HttpMethod? sentMethod = null;
        string? sentUri = null;
        bool hadContent = true;
        FakeOutputHandler output = new();
        ExportTemplateAtManagementGroupScopeDeploymentsProcessor processor =
            CreateProcessor(new ExportTemplateAtManagementGroupScopeDeploymentsProcessor(), new FakeHttpPipeline((request, _) =>
            {
                sentMethod = request.Method;
                sentUri = request.RequestUri!.AbsoluteUri;
                hadContent = request.Content is not null;
                return Json("""{"template":{"contentVersion":"1.0.0.0","resources":[]}}""");
            }), output);
        processor.ManagementGroupId = "test-management-group";
        processor.Name = "deployment_(blue)";

        await processor.ProcessAsync();

        Assert.Equal(HttpMethod.Post, sentMethod);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Management/managementGroups/test-management-group/providers/Microsoft.Resources/deployments/deployment_%28blue%29/exportTemplate?api-version=2025-04-01",
            sentUri);
        Assert.False(hadContent);
        System.Text.Json.JsonElement template =
            Assert.IsType<System.Text.Json.JsonElement>(Assert.Single(output.WrittenValues));
        Assert.Equal("1.0.0.0", template.GetProperty("contentVersion").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Array, template.GetProperty("resources").ValueKind);
    }

    [Fact]
    public void Export_CliMetadataMapsAdvertisedShortAliases()
    {
        CommandMetadata metadata = new Microsoft.Azure.Azclips.Extensions.Resources.ExtensionIndex()
            .GetCommandMetadata("deployment mg export", ClientType.CLI);

        Assert.Equal("management-group-id", metadata.ParameterAliases["m"]);
        Assert.Equal("name", metadata.ParameterAliases["n"]);
    }

    [Fact]
    public async Task Export_ThrowsWhenResponseHasNoTemplate()
    {
        FakeOutputHandler output = new();
        ExportTemplateAtManagementGroupScopeDeploymentsProcessor processor =
            CreateProcessor(new ExportTemplateAtManagementGroupScopeDeploymentsProcessor(), new FakeHttpPipeline((_, _) =>
                Json("{}")), output);
        processor.ManagementGroupId = "test-management-group";
        processor.Name = "test-deployment";

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());

        Assert.Contains("did not include a template", exception.Message);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Export_ArmFailureIsSurfacedWithoutOutput()
    {
        FakeHttpPipeline pipeline = new((_, _) =>
            Json(
                """{"error":{"code":"DeploymentNotFound","message":"The deployment was not found."}}""",
                HttpStatusCode.NotFound));
        FakeOutputHandler output = new();
        ExportTemplateAtManagementGroupScopeDeploymentsProcessor processor =
            CreateProcessor(new ExportTemplateAtManagementGroupScopeDeploymentsProcessor(), pipeline, output);
        processor.ManagementGroupId = "test-management-group";
        processor.Name = "missing-deployment";

        HttpRequestException exception =
            await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync());

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Contains("DeploymentNotFound", exception.Message);
        Assert.Equal(1, pipeline.SendCount);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task WhatIf_PollsLocationAndReturnsChangeListWithoutFinalGet()
    {
        List<string> requestUris = [];
        FakeHttpPipeline pipeline = new((request, _) =>
        {
            requestUris.Add(request.RequestUri!.AbsoluteUri);
            if (requestUris.Count == 1)
            {
                HttpResponseMessage accepted = Json("""{"status":"Running"}""", HttpStatusCode.Accepted);
                accepted.Headers.Location = new Uri("https://management.azure.com/operations/location-result");
                accepted.Headers.TryAddWithoutValidation(
                    "Azure-AsyncOperation",
                    "https://management.azure.com/operations/status-only");
                return accepted;
            }

            return Json(
                """{"status":"Succeeded","properties":{"changes":[{"resourceId":"/test","changeType":"NoChange"}]}}""");
        });
        FakeOutputHandler output = new();
        NewManagementGroupDeploymentWhatIfProcessor_Orchestrator processor = CreateProcessor(
            new NewManagementGroupDeploymentWhatIfProcessor_Orchestrator(),
            pipeline,
            output);

        await processor.ProcessAsync();

        Assert.Equal(2, pipeline.SendCount);
        Assert.EndsWith("/operations/location-result", requestUris[1]);
        WhatIfOperationResult result = Assert.IsType<WhatIfOperationResult>(Assert.Single(output.WrittenValues));
        Assert.Equal("NoChange", Assert.Single(result.Properties!.Changes!).ChangeType);
    }

    private static T CreateProcessor<T>(T processor, FakeHttpPipeline pipeline, FakeOutputHandler output)
        where T : ResourcesProcessorBase
    {
        Profile profile = new() { TenantId = "test-tenant", SubscriptionId = "test-subscription" };
        processor.Init(new ProcessorContext(
            pipeline,
            new FakeEventPublisher(),
            new FakeAuthHandler(profile),
            output,
            Profile: profile));

        switch (processor)
        {
            case NewManagementGroupDeploymentProcessor_Orchestrator create:
                create.ManagementGroupId = "test-management-group";
                create.Name = "test-deployment";
                create.Location = "eastus";
                create.TemplateObject = new { contentVersion = "1.0.0.0", resources = Array.Empty<object>() };
                break;
            case TestManagementGroupDeploymentProcessor_Orchestrator validate:
                validate.ManagementGroupId = "test-management-group";
                validate.Name = "test-deployment";
                validate.Location = "eastus";
                validate.TemplateObject = new { contentVersion = "1.0.0.0", resources = Array.Empty<object>() };
                break;
            case NewManagementGroupDeploymentWhatIfProcessor_Orchestrator whatIf:
                whatIf.ManagementGroupId = "test-management-group";
                whatIf.Name = "test-deployment";
                whatIf.Location = "eastus";
                whatIf.TemplateObject = new { contentVersion = "1.0.0.0", resources = Array.Empty<object>() };
                break;
        }

        return processor;
    }

    private static HttpResponseMessage Json(
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
}
