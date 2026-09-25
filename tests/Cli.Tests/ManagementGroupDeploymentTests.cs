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
            "deployment mg validate",
            "deployment mg what-if",
        })
        {
            CommandMetadata metadata = index.GetCommandMetadata(command, ClientType.CLI);
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
            ManagementGroupId = "contoso",
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
    public async Task PowerShellTest_WritesStructuredBadRequestResult()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json(
            """
            {
              "error": {
                "code": "InvalidTemplate",
                "message": "The template is invalid."
              }
            }
            """,
            HttpStatusCode.BadRequest));
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

        DeploymentValidateResult result =
            Assert.IsType<DeploymentValidateResult>(Assert.Single(output.WrittenValues));
        Assert.Equal("InvalidTemplate", result.Error?.Code);
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
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(3, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.EndsWith("/validate?api-version=2025-04-01", requests[0].Uri.ToString());
        Assert.All(requests.Skip(1), request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(location, request.Uri.ToString());
        });
    }

    [Fact]
    public async Task WhatIfAsync_ReturnsLocationPayloadWithoutFinalActionGet()
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
                _ => throw new InvalidOperationException("Unexpected final action request."),
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

        Assert.Equal(2, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.EndsWith("/whatIf?api-version=2025-04-01", requests[0].Uri.ToString());
        Assert.Equal(HttpMethod.Get, requests[1].Method);
        Assert.Equal(location, requests[1].Uri.ToString());
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
                2 => Json("""{ "status": "Succeeded" }"""),
                _ => throw new InvalidOperationException("Unexpected request."),
            };
        });
        var output = new FakeOutputHandler();
        var processor = new DeleteAtManagementGroupScopeDeploymentsProcessor
        {
            ManagementGroupId = "contoso",
            DeploymentName = "mg-test",
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(2, requests.Count);
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
