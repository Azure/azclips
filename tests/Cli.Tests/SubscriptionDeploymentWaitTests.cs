// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for subscription-scope deployment wait (az deployment sub wait): command wiring, polling outcomes and condition rules,
/// against a fake HTTP pipeline.
/// </summary>
public class SubscriptionDeploymentWaitTests
{
    private static WaitSubscriptionDeploymentProcessor_Orchestrator Wait(
        FakeHttpPipeline pipeline, FakeOutputHandler output, Action<WaitSubscriptionDeploymentProcessor_Orchestrator> configure) =>
        Configure(new WaitSubscriptionDeploymentProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = "dep";
            p.Interval = 1;
            configure(p);
        });

    [Theory]
    [InlineData("deployment sub wait", ClientType.CLI)]
    public void Command_MapsToProcessor(string command, ClientType client) =>
        AssertSubscriptionCommand<WaitSubscriptionDeploymentProcessor_Orchestrator>(command, client);

    [Fact]
    public async Task Wait_Created_ReturnsOnSucceeded_AndWritesNothing()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, SucceededSubscriptionDeployment));

        await Wait(pipeline, output, p => p.Created = true).ProcessAsync();

        Assert.Equal(SubscriptionDeploymentsUri + "/dep?api-version=2025-04-01", Assert.Single(sent).Uri);
        Assert.Empty(output.WrittenValues);
    }

    [Fact]
    public async Task Wait_Deleted_ReturnsOnNotFound()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.NotFound, """{"error":{"code":"DeploymentNotFound","message":"gone"}}"""));

        await Wait(pipeline, output, p => p.Deleted = true).ProcessAsync();

        Assert.Single(sent);
    }

    [Fact]
    public async Task Wait_FailedDeployment_Throws()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.OK, """{"name":"dep","properties":{"provisioningState":"Failed"}}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Wait(pipeline, output, p => p.Created = true).ProcessAsync());
    }

    [Fact]
    public async Task Wait_RequiresExactlyOneCondition()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.OK, SucceededSubscriptionDeployment));

        await Assert.ThrowsAsync<ArgumentException>(() => Wait(pipeline, output, p => { p.Created = true; p.Deleted = true; }).ProcessAsync());
        await Assert.ThrowsAsync<ArgumentException>(() => Wait(pipeline, output, _ => { }).ProcessAsync());

        Assert.Equal(0, pipeline.SendCount);
    }

    [Fact]
    public async Task Wait_ZeroTimeout_TimesOutWithoutRequest()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.OK, SucceededSubscriptionDeployment));

        await Assert.ThrowsAsync<TimeoutException>(() => Wait(pipeline, output, p => { p.Exists = true; p.Timeout = 0; }).ProcessAsync());

        Assert.Equal(0, pipeline.SendCount);
    }
}
