// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for subscription-scope deployment cancel (Stop-AzDeployment / az deployment sub cancel): command wiring and the
/// request sent, against a fake HTTP pipeline.
/// </summary>
public class SubscriptionDeploymentCancelTests
{
    [Theory]
    [InlineData("deployment sub cancel", ClientType.CLI)]
    [InlineData("Stop-AzDeployment", ClientType.PowerShell)]
    public void Command_MapsToProcessor(string command, ClientType client) =>
        AssertSubscriptionCommand<StopSubscriptionDeploymentProcessor>(command, client);

    [Fact]
    public async Task Cancel_PostsToCancelEndpoint()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.NoContent));

        await Configure(new StopSubscriptionDeploymentProcessor(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = "dep";
        }).ProcessAsync();

        Assert.Equal(HttpMethod.Post, sent[0].Method);
        Assert.Equal(SubscriptionDeploymentsUri + "/dep/cancel?api-version=2025-04-01", sent[0].Uri);
    }
}
