// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Cli.Tests;

public class ManagementGroupDeploymentTests
{
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
