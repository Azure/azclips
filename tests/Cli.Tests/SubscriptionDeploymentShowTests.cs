// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Cli.Tests.DeploymentProcessorTestKit;

namespace Cli.Tests;

/// <summary>
/// Offline tests for subscription-scope deployment show (Get-AzDeployment -Name/-Id / az deployment sub show): command wiring, the
/// request sent and id parsing, against a fake HTTP pipeline.
/// </summary>
public class SubscriptionDeploymentShowTests
{
    [Theory]
    [InlineData("deployment sub show", ClientType.CLI)]
    [InlineData("Get-AzDeployment_Get", ClientType.PowerShell)]
    [InlineData("Get-AzDeployment_GetById", ClientType.PowerShell)]
    public void Command_MapsToProcessor(string command, ClientType client) =>
        AssertSubscriptionCommand<GetSubscriptionDeploymentProcessor_Orchestrator>(command, client);

    [Fact]
    public async Task Show_ByName_GetsDeployment()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, SucceededSubscriptionDeployment));

        await Configure(new GetSubscriptionDeploymentProcessor_Orchestrator(), pipeline, output, p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.Name = "dep";
        }).ProcessAsync();

        Assert.Equal(HttpMethod.Get, sent[0].Method);
        Assert.Equal(SubscriptionDeploymentsUri + "/dep?api-version=2025-04-01", sent[0].Uri);
    }

    [Fact]
    public async Task Show_ById_ParsesSubscriptionAndName()
    {
        var (pipeline, output, sent) = Fake(_ => Status(HttpStatusCode.OK, SucceededSubscriptionDeployment));

        await Configure(new GetSubscriptionDeploymentProcessor_Orchestrator(), pipeline, output, p =>
            p.Id = $"/subscriptions/{SubscriptionId}/providers/Microsoft.Resources/deployments/dep").ProcessAsync();

        Assert.Equal(SubscriptionDeploymentsUri + "/dep?api-version=2025-04-01", Assert.Single(sent).Uri);
    }

    [Fact]
    public async Task Show_ByResourceGroupScopedId_ThrowsWithoutRequest()
    {
        var (pipeline, output, _) = Fake(_ => Status(HttpStatusCode.OK, SucceededSubscriptionDeployment));

        await Assert.ThrowsAsync<InvalidInputException>(() => Configure(new GetSubscriptionDeploymentProcessor_Orchestrator(), pipeline, output, p =>
            p.Id = $"/subscriptions/{SubscriptionId}/resourceGroups/rg/providers/Microsoft.Resources/deployments/dep").ProcessAsync());

        Assert.Equal(0, pipeline.SendCount);
    }
}
