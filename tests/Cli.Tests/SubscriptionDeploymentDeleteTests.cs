// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for subscription-scope deployment delete (Remove-AzDeployment / az deployment sub delete): command wiring and the
/// request sent, against a fake HTTP pipeline.
/// </summary>
public class SubscriptionDeploymentDeleteTests
{
    [Theory]
    [InlineData("deployment sub delete", ClientType.CLI)]
    [InlineData("Remove-AzDeployment", ClientType.PowerShell)]
    public void Command_MapsToProcessor(string command, ClientType client) =>
        AssertSubscriptionCommand<RemoveSubscriptionDeploymentProcessor>(command, client);

    [Fact]
    public async Task Delete_SendsDeleteToDeployment()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.NoContent));

        await Configure(new RemoveSubscriptionDeploymentProcessor(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = "dep";
        }).ProcessAsync();

        Assert.Equal(HttpMethod.Delete, sent[0].Method);
        Assert.Equal(SubscriptionDeploymentsUri + "/dep?api-version=2025-04-01", sent[0].Uri);
    }
}
