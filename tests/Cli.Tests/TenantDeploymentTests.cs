// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Xunit;

namespace Cli.Tests;

public class TenantDeploymentTests
{
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
    public void ExtensionIndex_ExposesRequestedTenantCommands()
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
            index.GetCommandKeys(ClientType.CLI)
                .Where(key => key.StartsWith("deployment tenant ", StringComparison.Ordinal))
                .Order()
                .ToArray());

        foreach (string command in new[]
        {
            "deployment tenant create",
            "deployment tenant validate",
            "deployment tenant what-if",
        })
        {
            CommandMetadata metadata = index.GetCommandMetadata(command, ClientType.CLI);
            Assert.DoesNotContain("no-prompt", metadata.Parameters.Keys);
        }

        CommandMetadata whatIfMetadata =
            index.GetCommandMetadata("deployment tenant what-if", ClientType.CLI);
        Assert.DoesNotContain("no-pretty-print", whatIfMetadata.Parameters.Keys);
        Assert.DoesNotContain("no-color", whatIfMetadata.Parameters.Keys);

        Assert.Equal(
            [
                "Get-AzTenantDeployment_Get",
                "Get-AzTenantDeployment_List",
                "New-AzTenantDeployment",
                "Remove-AzTenantDeployment",
                "Stop-AzTenantDeployment",
                "Test-AzTenantDeployment",
            ],
            index.GetCommandKeys(ClientType.PowerShell)
                .Where(key => key.Contains("TenantDeployment", StringComparison.Ordinal))
                .Order()
                .ToArray());
    }

    [Fact]
    public async Task Create_NoWait_ValidatesThenSendsTenantBody()
    {
        List<(HttpMethod Method, Uri Uri, string? Body)> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add((request.Method, request.RequestUri!, request.Content?.ReadAsStringAsync().GetAwaiter().GetResult()));
            return requests.Count == 1
                ? Json("""{ "name": "tenant-test" }""")
                : Json("""{ "name": "tenant-test", "location": "eastus", "properties": { "provisioningState": "Accepted" } }""");
        });
        var output = new FakeOutputHandler();
        var processor = new NewTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object>
            {
                ["$schema"] = "https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#",
                ["contentVersion"] = "1.0.0.0",
                ["resources"] = Array.Empty<object>(),
            },
            NoWait = true,
        };
        Init(processor, pipeline, output);

        await processor.ProcessAsync();

        Assert.Equal(2, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.EndsWith("/validate?api-version=2025-04-01", requests[0].Uri.ToString());
        Assert.Equal(HttpMethod.Put, requests[1].Method);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Resources/deployments/tenant-test?api-version=2025-04-01",
            requests[1].Uri.ToString());

        JsonElement body = JsonDocument.Parse(requests[1].Body!).RootElement;
        Assert.Equal("eastus", body.GetProperty("location").GetString());
        Assert.Equal("Incremental", body.GetProperty("properties").GetProperty("mode").GetString());
        Assert.Equal("Provider", body.GetProperty("properties").GetProperty("validationLevel").GetString());
    }

    [Fact]
    public async Task Create_WaitsOnLocationThenReadsFinalDeployment()
    {
        List<Uri> requests = [];
        var pipeline = new FakeHttpPipeline((request, _) =>
        {
            requests.Add(request.RequestUri!);
            return requests.Count switch
            {
                1 => Json("""{ "name": "tenant-test" }"""),
                2 => WithLocation(
                    Json("""{ "status": "Accepted" }""", HttpStatusCode.Accepted),
                    "https://management.azure.com/providers/Microsoft.Resources/operations/create-1"),
                3 => Json("""{ "status": "Succeeded" }"""),
                4 => Json(
                    """{ "name": "tenant-test", "properties": { "provisioningState": "Succeeded" } }"""),
                _ => throw new InvalidOperationException("Unexpected request."),
            };
        });
        var processor = new NewTenantDeploymentProcessor_Orchestrator
        {
            Name = "tenant-test",
            Location = "eastus",
            TemplateObject = new Dictionary<string, object>
            {
                ["resources"] = Array.Empty<object>(),
            },
        };
        Init(processor, pipeline, new FakeOutputHandler());

        await processor.ProcessAsync();

        Assert.Equal(4, requests.Count);
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Resources/operations/create-1",
            requests[2].ToString());
        Assert.Equal(
            "https://management.azure.com/providers/Microsoft.Resources/deployments/tenant-test?api-version=2025-04-01",
            requests[3].ToString());
    }

    [Fact]
    public async Task List_UnwrapsTenantDeploymentPage()
    {
        var pipeline = new FakeHttpPipeline((_, _) => Json("""
            {
              "value": [
                { "name": "one", "properties": { "provisioningState": "Succeeded" } },
                { "name": "two", "properties": { "provisioningState": "Failed" } }
              ]
            }
            """));
        var output = new FakeOutputHandler();
        var processor = new ListAtTenantScopeDeploymentsProcessor();
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
        var processor = new StopTenantDeploymentProcessor_Orchestrator { Name = "done" };
        Init(processor, pipeline, new FakeOutputHandler());

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => processor.ProcessAsync());

        Assert.Contains("not running", error.Message);
        Assert.Equal(1, pipeline.SendCount);
    }

    [Fact]
    public async Task TestValidation_WritesStructuredBadRequestResult()
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
        var processor = new TestTenantDeploymentProcessor_Orchestrator
        {
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
        Assert.Equal("The template is invalid.", result.Error?.Message);
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
        var processor = new DeleteAtTenantScopeDeploymentsProcessor
        {
            DeploymentName = "tenant-test",
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
        var processor = new WaitTenantDeploymentProcessor
        {
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
        var processor = new WaitTenantDeploymentProcessor
        {
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
