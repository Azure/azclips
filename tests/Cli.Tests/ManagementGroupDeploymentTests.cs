// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Deployment.Customization;
using Microsoft.Azure.Azclips.Extensions.Deployment.Models;

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
        NewManagementGroupDeploymentProcessor processor = CreateProcessor(
            new NewManagementGroupDeploymentProcessor { NoWait = true },
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
        NewManagementGroupDeploymentProcessor processor = CreateProcessor(
            new NewManagementGroupDeploymentProcessor { NoWait = true },
            pipeline,
            new FakeOutputHandler());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync());

        Assert.Equal(1, pipeline.SendCount);
        Assert.Contains("InvalidTemplate: Template is invalid.", exception.Message);
    }

    private static T CreateProcessor<T>(T processor, FakeHttpPipeline pipeline, FakeOutputHandler output)
        where T : Microsoft.Azure.Azclips.Extensions.Deployment.Processors.DeploymentProcessorBase
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
            case NewManagementGroupDeploymentProcessor create:
                create.ManagementGroupId = "test-management-group";
                create.Name = "test-deployment";
                create.Location = "eastus";
                create.TemplateObject = new { contentVersion = "1.0.0.0", resources = Array.Empty<object>() };
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
