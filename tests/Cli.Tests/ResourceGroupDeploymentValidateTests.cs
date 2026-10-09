// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline processor tests for deployment validate (Test-AzResourceGroupDeployment / az deployment group validate): the request sent, response
/// handling, errors and output, against a fake HTTP pipeline.
/// </summary>
public class ResourceGroupDeploymentValidateTests
{
    private static TestResourceGroupDeploymentProcessor_Orchestrator Validate(
        FakeHttpPipeline pipeline, FakeOutputHandler output, Action<TestResourceGroupDeploymentProcessor_Orchestrator>? configure = null) =>
        Configure(new TestResourceGroupDeploymentProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = "test-rg";
            p.Name = "dep";
            p.TemplateUri = "https://example.com/t.json";
            p.Parameter = ["p1=v1", "count=3"];
            p.Mode = "Complete";
            p.RollbackToDeploymentName = "previous";
            configure?.Invoke(p);
        });

    [Fact]
    public async Task Validate_PostsSerializedBody_AndWritesValidateResult()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK,
            """{"id":"/x/dep","name":"dep","properties":{"provisioningState":"Succeeded","mode":"Complete"}}"""));

        await Validate(pipeline, output).ProcessAsync();

        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(DeploymentUri + "/validate?api-version=2025-04-01", request.Uri);

        var properties = JsonDocument.Parse(request.Body!).RootElement.GetProperty("properties");
        Assert.Equal("Complete", properties.GetProperty("mode").GetString());
        Assert.Equal("https://example.com/t.json", properties.GetProperty("templateLink").GetProperty("uri").GetString());
        Assert.Equal("v1", properties.GetProperty("parameters").GetProperty("p1").GetProperty("value").GetString());
        Assert.Equal(3, properties.GetProperty("parameters").GetProperty("count").GetProperty("value").GetInt32());
        var onError = properties.GetProperty("onErrorDeployment");
        Assert.Equal("SpecificDeployment", onError.GetProperty("type").GetString());
        Assert.Equal("previous", onError.GetProperty("deploymentName").GetString());

        var result = Assert.IsType<DeploymentValidateResult>(Assert.Single(output.WrittenValues));
        Assert.Null(result.Error);
        Assert.Equal("Succeeded", result.Properties!.ProvisioningState);
    }

    [Fact]
    public async Task Validate_ArmValidationError400_ThrowsFlattenedMessage_AndWritesNothing()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.BadRequest, """
            {"error":{"code":"InvalidTemplate","message":"Template is invalid.","target":"resources[0]",
              "details":[{"code":"InvalidParameter","message":"Parameter 'p1' is not allowed."}]}}
            """));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Validate(pipeline, output).ProcessAsync());

        Assert.Single(sent);
        Assert.StartsWith("Deployment validation failed.", error.Message);
        Assert.Contains("InvalidTemplate: Template is invalid. (target: resources[0])", error.Message);
        Assert.Contains("InvalidParameter: Parameter 'p1' is not allowed.", error.Message);
        Assert.DoesNotContain("{", error.Message);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Validate_AcceptedWithoutLocation_Throws()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.Accepted));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Validate(pipeline, output).ProcessAsync());

        Assert.Contains("Location", error.Message);
    }

    [Fact]
    public async Task Validate_NonValidationArmError_ThrowsHttpError()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.Forbidden, """{"error":{"code":"AuthorizationFailed","message":"denied"}}"""));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Validate(pipeline, output).ProcessAsync());

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task Validate_BothRollbackOptions_ThrowsBeforeSending()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsAsync<ArgumentException>(() => Validate(pipeline, output, p => p.RollbackOnError = true).ProcessAsync());

        Assert.Equal(0, pipeline.SendCount);
    }
}
