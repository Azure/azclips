// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Xunit;

namespace Cli.Tests;

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
            Assert.DoesNotContain("mode", metadata.Parameters.Keys);
            Assert.DoesNotContain("no-prompt", metadata.Parameters.Keys);
            Assert.DoesNotContain("no-pretty-print", metadata.Parameters.Keys);
            Assert.DoesNotContain("no-color", metadata.Parameters.Keys);
        }

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

        var deployment = Assert.IsType<ScopedDeployment>(method.Invoke(processor, null));

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
    public async Task Stop_RejectsTerminalDeploymentBeforeCancel()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json(
            """{ "name": "done", "properties": { "provisioningState": "Succeeded" } }"""));
        var processor = new StopManagementGroupDeploymentProcessor_Orchestrator
        {
            ManagementGroupId = "contoso",
            Name = "done",
        };
        Init(processor, pipeline, new FakeOutputHandler());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => processor.ProcessAsync());

        Assert.Contains("not running", error.Message);
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
    public async Task ValidateAsync_FetchesFinalResultFromLocation()
    {
        List<(HttpMethod Method, Uri Uri)> requests = [];
        const string location =
            "https://management.azure.com/providers/Microsoft.Resources/operations/validate-1";
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!));
            return requests.Count switch
            {
                1 => WithLocation(
                    Json("""{ "status": "Accepted" }""", HttpStatusCode.Accepted),
                    location),
                2 => Json("""{ "status": "Succeeded" }"""),
                3 => Json(
                    """{ "name": "validated", "properties": { "provisioningState": "Succeeded" } }"""),
                _ => throw new InvalidOperationException("Unexpected request."),
            };
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

        Assert.Equal(3, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.EndsWith("/validate?api-version=2025-04-01", requests[0].Uri.ToString());
        Assert.All(requests.Skip(1), request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(location, request.Uri.ToString());
        });
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
    public async Task WhatIfAsync_ThrowsWhenStatusFailedWithoutError()
    {
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
        Init(processor, pipeline, new FakeOutputHandler());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());

        Assert.Equal("Management group deployment what-if failed.", exception.Message);
    }

    [Fact]
    public async Task CreateWhatIf_ThrowsWhenStatusFailedWithoutError()
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
        Init(processor, pipeline, new FakeOutputHandler());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());

        Assert.Equal("Management group deployment what-if failed.", exception.Message);
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
        var processor = new DeleteAtManagementGroupScopeDeploymentsProcessor_Customization
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
        var processor = new WaitManagementGroupDeploymentProcessor
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
        var processor = new WaitManagementGroupDeploymentProcessor
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

    private static HttpResponseMessage WithLocation(HttpResponseMessage response, string location)
    {
        response.Headers.Location = new Uri(location);
        return response;
    }
}
