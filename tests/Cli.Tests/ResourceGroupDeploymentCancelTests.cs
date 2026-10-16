// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline processor tests for deployment cancel (Stop-AzResourceGroupDeployment / az deployment group cancel): the request sent, response
/// handling, errors and output, against a fake HTTP pipeline.
/// </summary>
public class ResourceGroupDeploymentCancelTests
{
    private static StopResourceGroupDeploymentProcessor Stop(FakeHttpPipeline pipeline, FakeOutputHandler output) =>
        Configure(new StopResourceGroupDeploymentProcessor(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = "test-rg";
            p.Name = "dep";
        });

    [Fact]
    public async Task Cancel_PostsToCancelEndpoint_AndWritesNothingOnNoContent()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.NoContent));

        await Stop(pipeline, output).ProcessAsync();

        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(DeploymentUri + "/cancel?api-version=2025-04-01", request.Uri);
        Assert.Empty(output.WrittenValues);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]   // deployment is not running
    [InlineData(HttpStatusCode.NotFound)]   // deployment does not exist
    public async Task Cancel_ArmError_ThrowsWithStatusAndArmMessage(HttpStatusCode code)
    {
        var (pipeline, output, _) = Fake(_ => Status(code, """{"error":{"code":"DeploymentCannotBeCancelled","message":"not running"}}"""));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Stop(pipeline, output).ProcessAsync());

        Assert.Equal(code, error.StatusCode);
        Assert.Contains("DeploymentCannotBeCancelled", error.Message);
        Assert.Empty(output.WrittenValues);
    }
}
