// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline processor tests for deployment delete (Remove-AzResourceGroupDeployment / az deployment group delete): the request sent, response
/// handling, errors and output, against a fake HTTP pipeline.
/// </summary>
public class ResourceGroupDeploymentDeleteTests
{
    private static RemoveResourceGroupDeploymentProcessor Remove(FakeHttpPipeline pipeline, FakeOutputHandler output) =>
        Configure(new RemoveResourceGroupDeploymentProcessor(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = "test-rg";
            p.Name = "dep";
        });

    [Fact]
    public async Task Delete_SendsDelete_PollsLocationUntilNoContent_AndWritesNothing()
    {
        const string location = "https://management.azure.com/operationResults/delete-op?api-version=2025-04-01";
        int polls = 0;
        var (pipeline, output, sent) = Fake(request =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                var accepted = Status(HttpStatusCode.Accepted);
                accepted.Headers.Location = new Uri(location);
                return accepted;
            }

            // First poll: still running; second poll: done.
            if (++polls == 1)
            {
                var running = Status(HttpStatusCode.Accepted);
                running.Headers.Location = new Uri(location);
                return running;
            }
            return Status(HttpStatusCode.NoContent);
        });

        await Remove(pipeline, output).ProcessAsync();

        Assert.Equal(HttpMethod.Delete, sent[0].Method);
        Assert.Equal(DeploymentUri + "?api-version=2025-04-01", sent[0].Uri);
        Assert.All(sent.Skip(1), poll =>
        {
            Assert.Equal(HttpMethod.Get, poll.Method);
            Assert.Equal(location, poll.Uri);
        });
        Assert.Equal(2, polls);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Delete_MissingDeployment_NoContent_SucceedsWithoutPolling()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.NoContent));

        await Remove(pipeline, output).ProcessAsync();

        Assert.Single(sent);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Delete_ArmConflict_Throws()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.Conflict, """{"error":{"code":"DeploymentActive","message":"still running"}}"""));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Remove(pipeline, output).ProcessAsync());

        Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
        Assert.Contains("DeploymentActive", error.Message);
    }

    [Fact]
    public async Task DeleteById_SendsDeleteForParsedDeployment_IdSubscriptionWins()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.NoContent));

        await Configure(new RemoveResourceGroupDeploymentProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = "other-sub";
            p.Id = $"/subscriptions/{SubscriptionId}/resourceGroups/test-rg/providers/Microsoft.Resources/deployments/dep/";
        }).ProcessAsync();

        Assert.Equal(HttpMethod.Delete, sent[0].Method);
        Assert.Equal(DeploymentUri + "?api-version=2025-04-01", sent[0].Uri);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("not-a-resource-id")]
    [InlineData("/subscriptions/sub/resourceGroups/test-rg/providers/Microsoft.Compute/virtualMachines/vm1")]
    public async Task DeleteById_MalformedId_ThrowsWithoutRequest(string id)
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.NoContent));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Configure(new RemoveResourceGroupDeploymentProcessor_Orchestrator(), pipeline, output, p => p.Id = id).ProcessAsync());
        Assert.Empty(sent);
    }
}
